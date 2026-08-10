using Azure.Identity;
using DmarcAnalyzer.Core.Abstractions;
using DmarcAnalyzer.Core.Entities;
using DmarcAnalyzer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph;

namespace DmarcAnalyzer.Infrastructure.Graph;

/// <summary>
/// Builds a <see cref="GraphServiceClient"/> from the currently-configured app registration
/// (Entra tenant/client ID in SQL, client secret in Key Vault). Built fresh per call rather than
/// cached, since polling happens at most every few minutes and this keeps secret-rotation trivial
/// (no cache to invalidate).
/// </summary>
public class GraphClientFactory(DmarcAnalyzerDbContext db, ISecretStore secretStore)
{
    private static readonly string[] GraphDefaultScope = ["https://graph.microsoft.com/.default"];

    public async Task<GraphServiceClient?> CreateClientAsync(CancellationToken cancellationToken = default)
    {
        var settings = await db.GraphConnectionSettings.FindAsync([GraphConnectionSettings.SingletonId], cancellationToken);
        if (settings is null)
        {
            return null;
        }

        var clientSecret = await secretStore.GetSecretAsync(settings.ClientSecretKeyVaultName, cancellationToken);
        if (string.IsNullOrEmpty(clientSecret))
        {
            return null;
        }

        var credential = new ClientSecretCredential(
            settings.TenantId.ToString(),
            settings.ClientId.ToString(),
            clientSecret);

        return new GraphServiceClient(credential, GraphDefaultScope);
    }
}
