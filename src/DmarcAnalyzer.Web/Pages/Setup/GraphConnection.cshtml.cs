using System.ComponentModel.DataAnnotations;
using Azure.Identity;
using DmarcAnalyzer.Core.Abstractions;
using DmarcAnalyzer.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph;
using GraphConnectionSettingsEntity = DmarcAnalyzer.Core.Entities.GraphConnectionSettings;

namespace DmarcAnalyzer.Web.Pages.Setup;

public class GraphConnectionModel(DmarcAnalyzerDbContext db, ISecretStore secretStore) : PageModel
{
    private const string ClientSecretName = "GraphClientSecret";

    [BindProperty]
    [Required(ErrorMessage = "Tenant ID is required.")]
    [Display(Name = "Entra ID Tenant ID")]
    public string TenantId { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Client ID is required.")]
    [Display(Name = "App registration Client ID")]
    public string ClientId { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Client secret is required.")]
    [DataType(DataType.Password)]
    [Display(Name = "Client secret value")]
    public string ClientSecret { get; set; } = string.Empty;

    public string? ValidationError { get; private set; }
    public bool AlreadyConfigured { get; private set; }

    public async Task OnGetAsync()
    {
        var existing = await db.GraphConnectionSettings.FindAsync(GraphConnectionSettingsEntity.SingletonId);
        if (existing is not null)
        {
            TenantId = existing.TenantId.ToString();
            ClientId = existing.ClientId.ToString();
            AlreadyConfigured = true;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (!Guid.TryParse(TenantId, out var tenantGuid) || !Guid.TryParse(ClientId, out var clientGuid))
        {
            ValidationError = "Tenant ID and Client ID must both be valid GUIDs.";
            return Page();
        }

        try
        {
            var credential = new ClientSecretCredential(tenantGuid.ToString(), clientGuid.ToString(), ClientSecret);
            var testClient = new GraphServiceClient(credential, ["https://graph.microsoft.com/.default"]);
            await testClient.Organization.GetAsync(config =>
            {
                config.QueryParameters.Top = 1;
                config.QueryParameters.Select = ["id"];
            });
        }
        catch (Exception ex)
        {
            ValidationError = $"Could not authenticate to Microsoft Graph with these credentials: {ex.Message}";
            return Page();
        }

        await secretStore.SetSecretAsync(ClientSecretName, ClientSecret);

        var settings = await db.GetOrCreateSingletonAsync(
            GraphConnectionSettingsEntity.SingletonId,
            id => new GraphConnectionSettingsEntity { Id = id });

        settings.TenantId = tenantGuid;
        settings.ClientId = clientGuid;
        settings.ClientSecretKeyVaultName = ClientSecretName;
        settings.ConfiguredUtc = DateTime.UtcNow;
        settings.LastValidatedUtc = DateTime.UtcNow;
        settings.LastValidationSucceeded = true;
        settings.LastValidationError = null;

        await db.SaveChangesAsync();

        return RedirectToPage("Domains");
    }
}
