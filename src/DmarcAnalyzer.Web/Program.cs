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
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

var connectionString = builder.Configuration.GetConnectionString("DmarcAnalyzer")
    ?? throw new InvalidOperationException("Connection string 'DmarcAnalyzer' is not configured.");
builder.Services.AddDbContext<DmarcAnalyzerDbContext>(options => options.UseSqlServer(connectionString));

builder.Services.Configure<KeyVaultOptions>(builder.Configuration.GetSection(KeyVaultOptions.SectionName));

builder.Services.AddSingleton<ILookupClient>(new LookupClient());

builder.Services.AddScoped<ISecretStore, KeyVaultSecretStore>();
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

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseMiddleware<SetupGateMiddleware>();

app.UseAuthorization();

app.MapRazorPages();
app.MapChartDataEndpoints();

app.Run();
