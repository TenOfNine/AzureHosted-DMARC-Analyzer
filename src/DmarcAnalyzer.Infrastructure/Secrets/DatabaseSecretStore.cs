using System.Security.Cryptography;
using DmarcAnalyzer.Core.Abstractions;
using DmarcAnalyzer.Core.Entities;
using DmarcAnalyzer.Infrastructure.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace DmarcAnalyzer.Infrastructure.Secrets;

/// <summary>
/// Self-contained <see cref="ISecretStore"/> for deployments without Key Vault (e.g. Docker
/// Compose): values are encrypted with ASP.NET Core Data Protection and stored alongside the
/// app's other data, so no extra external service is required. The Data Protection key ring must
/// be persisted outside the container (see Program.cs) or secrets become unreadable on restart.
/// </summary>
public class DatabaseSecretStore(DmarcAnalyzerDbContext db, IDataProtectionProvider dataProtectionProvider) : ISecretStore
{
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("DmarcAnalyzer.SecretStore");

    public async Task SetSecretAsync(string name, string value, CancellationToken cancellationToken = default)
    {
        var cipherText = _protector.Protect(value);
        var existing = await db.Secrets.FindAsync([name], cancellationToken);
        if (existing is null)
        {
            db.Secrets.Add(new EncryptedSecret { Name = name, CipherText = cipherText });
        }
        else
        {
            existing.CipherText = cipherText;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        var entity = await db.Secrets.AsNoTracking().FirstOrDefaultAsync(s => s.Name == name, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(entity.CipherText);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
