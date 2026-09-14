namespace DmarcAnalyzer.Core.Entities;

/// <summary>
/// Backing store for <see cref="Abstractions.ISecretStore"/>'s database-backed implementation.
/// CipherText is produced by ASP.NET Core Data Protection — never a plaintext secret at rest.
/// </summary>
public class EncryptedSecret
{
    public string Name { get; set; } = string.Empty;
    public string CipherText { get; set; } = string.Empty;
}
