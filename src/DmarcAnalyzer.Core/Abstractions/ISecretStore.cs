namespace DmarcAnalyzer.Core.Abstractions;

/// <summary>Secret storage abstraction (Key Vault in production) — the Graph client secret is never stored in SQL.</summary>
public interface ISecretStore
{
    Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default);
    Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default);
}
