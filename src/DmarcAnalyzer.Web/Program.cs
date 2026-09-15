using DmarcAnalyzer.Core.Abstractions;
using DmarcAnalyzer.Core.Dkim;
using DmarcAnalyzer.Core.Legitimacy;
using DmarcAnalyzer.Core.Spf;
using DmarcAnalyzer.Infrastructure.Data;
using DmarcAnalyzer.Infrastructure.Dns;
using DmarcAnalyzer.Infrastructure.Graph;
using DmarcAnalyzer.Infrastructure.Ingestion;
using DmarcAnalyzer.Infrastructure.Retention;
using DmarcAnalyzer.Infrastructure.Secrets;
using DmarcAnalyzer.Infrastructure.Setup;
using DmarcAnalyzer.Web.Api;
using DmarcAnalyzer.Web.Infrastructure;
using DnsClient;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Suppress Kestrel's "Server" response header — no reason to advertise the runtime to an attacker.
builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);

builder.Services.AddRazorPages();

var connectionString = builder.Configuration.GetConnectionString("DmarcAnalyzer")
    ?? throw new InvalidOperationException("Connection string 'DmarcAnalyzer' is not configured.");
builder.Services.AddDbContext<DmarcAnalyzerDbContext>(options => options.UseSqlServer(connectionString));

// Data Protection keys must survive container restarts, or both the auth cookie and any secrets
// written by DatabaseSecretStore become unreadable every time the container is recreated. Azure App
// Service already persists Data Protection keys itself, so this is only needed (and only configured)
// when a key path is explicitly set, e.g. by docker-compose.yml pointing at a named volume.
var dataProtectionKeyPath = builder.Configuration["DataProtection:KeyPath"];
if (!string.IsNullOrEmpty(dataProtectionKeyPath))
{
    builder.Services.AddDataProtection()
        .SetApplicationName("DmarcAnalyzer")
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyPath));
}

builder.Services.Configure<KeyVaultOptions>(builder.Configuration.GetSection(KeyVaultOptions.SectionName));

builder.Services.AddSingleton<ILookupClient>(new LookupClient());

// Key Vault requires Azure; the database-backed store is the default so the app runs fully
// standalone (e.g. Docker Compose) without any external secret service. Azure deployments opt back
// into Key Vault via the SecretStore__Provider app setting (see infra/modules/webApp.bicep).
var secretStoreProvider = builder.Configuration["SecretStore:Provider"];
if (string.Equals(secretStoreProvider, "KeyVault", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddScoped<ISecretStore, KeyVaultSecretStore>();
}
else
{
    builder.Services.AddScoped<ISecretStore, DatabaseSecretStore>();
}

builder.Services.AddScoped<GraphClientFactory>();
builder.Services.AddScoped<IGraphMailboxClient, GraphMailboxClient>();
builder.Services.AddScoped<ISpfDnsResolver, DnsClientSpfResolver>();
builder.Services.AddScoped<IDkimDnsResolver, DnsClientDkimResolver>();
builder.Services.AddScoped<IReverseDnsResolver, DnsClientReverseDnsResolver>();
builder.Services.AddScoped<SpfEvaluator>();
builder.Services.AddScoped<DkimSelectorChecker>();
builder.Services.AddScoped<IDmarcReportIngestionPipeline, DmarcReportIngestionPipeline>();
builder.Services.AddScoped<ISetupStateService, SetupStateService>();

builder.Services.Configure<IngestionOptions>(builder.Configuration.GetSection(IngestionOptions.SectionName));
builder.Services.Configure<RetentionOptions>(builder.Configuration.GetSection(RetentionOptions.SectionName));
builder.Services.AddHostedService<MailboxPollingService>();
builder.Services.AddHostedService<RetentionPurgeService>();

var appInsightsConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
if (!string.IsNullOrEmpty(appInsightsConnectionString))
{
    builder.Services.AddApplicationInsightsTelemetry(options => options.ConnectionString = appInsightsConnectionString);
}

// Azure deployments gate every request via App Service Easy Auth at the platform level (see
// infra/modules/webApp.bicep) and need nothing here. A self-hosted deployment (Docker Compose) has
// no such platform, so it configures a generic OpenID Connect provider instead — any
// standards-compliant IdP works (Keycloak, Authentik, Auth0, or still Entra ID), since this is
// plain OIDC, not an Azure-specific integration.
var oidcAuthority = builder.Configuration["Authentication:Oidc:Authority"];
var oidcEnabled = !string.IsNullOrEmpty(oidcAuthority);
if (oidcEnabled)
{
    builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        })
        .AddCookie()
        .AddOpenIdConnect(options =>
        {
            options.Authority = oidcAuthority;
            options.ClientId = builder.Configuration["Authentication:Oidc:ClientId"];
            options.ClientSecret = builder.Configuration["Authentication:Oidc:ClientSecret"];
            options.ResponseType = "code";
            options.SaveTokens = true;
            options.Scope.Add("profile");
            options.Scope.Add("email");
        });

    builder.Services.AddAuthorization(options =>
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
    });
}

var app = builder.Build();

if (!oidcEnabled && !app.Environment.IsDevelopment())
{
    app.Logger.LogWarning(
        "Authentication:Oidc:Authority is not configured — this deployment has no sign-in requirement. " +
        "Configure an OIDC provider before exposing it publicly.");
}

// Self-hosted deployments apply pending EF Core migrations on startup rather than through a
// separate deploy step (Azure uses the EF bundle in deploy.yml instead, so this defaults to off).
// The SQL container may still be starting even after its healthcheck passes, hence the retry.
if (builder.Configuration.GetValue<bool>("Database:AutoMigrate"))
{
    using var migrationScope = app.Services.CreateScope();
    var db = migrationScope.ServiceProvider.GetRequiredService<DmarcAnalyzerDbContext>();
    const int maxAttempts = 10;
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            await db.Database.MigrateAsync();
            break;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            app.Logger.LogWarning(ex, "Database migration attempt {Attempt}/{MaxAttempts} failed, retrying...", attempt, maxAttempts);
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Registered before UseStaticFiles so hardening headers (CSP, X-Frame-Options, etc.) also cover
// static assets, not just Razor Pages responses.
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseStaticFiles();

app.UseRouting();

if (oidcEnabled)
{
    app.UseAuthentication();
}

app.UseAuthorization();

app.UseMiddleware<SetupGateMiddleware>();

app.MapRazorPages();
app.MapChartDataEndpoints();

if (oidcEnabled)
{
    app.MapGet("/account/signout", async (HttpContext context) =>
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await context.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);
    });
}

app.Run();
