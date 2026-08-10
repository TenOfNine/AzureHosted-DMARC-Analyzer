using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using DmarcAnalyzer.Core.Abstractions;
using Microsoft.Extensions.Options;

namespace DmarcAnalyzer.Infrastructure.Secrets;

/// <summary>
/// The Key Vault URI is only known once Bicep has deployed the vault and set it as an app setting — it
/// is never guaranteed to be present (e.g. local development). The client is built lazily so the app
/// can still start and serve the setup wizard; only an actual secret read/write fails clearly if
/// Key Vault isn't configured, rather than the whole DI container failing to build.
/// </summary>
public class KeyVaultSecretStore(IOptions<KeyVaultOptions> options) : ISecretStore
{
    private readonly Lazy<SecretClient> _client = new(() =>
    {
        var uri = options.Value.Uri;
        if (string.IsNullOrEmpty(uri))
        {
            throw new InvalidOperationException("Key Vault is not configured (KeyVault:Uri is empty).");
        }

        return new SecretClient(new Uri(uri), new DefaultAzureCredential());
    });

    public async Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default)
    {
        await _client.Value.SetSecretAsync(name, value, cancellationToken);
    }

    public async Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            var secret = await _client.Value.GetSecretAsync(name, cancellationToken: cancellationToken);
            return secret.Value.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }
}
