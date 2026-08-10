namespace DmarcAnalyzer.Core.Entities;

/// <summary>
/// Singleton row (Id is always 1) holding the Entra ID app registration used for Graph app-only auth.
/// The client secret value itself is never stored here — only the Key Vault secret name that points to it.
/// </summary>
public class GraphConnectionSettings
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public Guid TenantId { get; set; }
    public Guid ClientId { get; set; }
    public string ClientSecretKeyVaultName { get; set; } = string.Empty;
    public DateTime ConfiguredUtc { get; set; }
    public DateTime? LastValidatedUtc { get; set; }
    public bool LastValidationSucceeded { get; set; }
    public string? LastValidationError { get; set; }
}
