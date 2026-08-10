using DmarcAnalyzer.Core.Abstractions;
using DmarcAnalyzer.Core.Entities;
using DmarcAnalyzer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DmarcAnalyzer.Infrastructure.Setup;

public class SetupStateService(DmarcAnalyzerDbContext db) : ISetupStateService
{
    public async Task<bool> IsSetupCompleteAsync(CancellationToken cancellationToken = default)
    {
        var hasGraphConnection = await db.GraphConnectionSettings
            .AnyAsync(g => g.Id == GraphConnectionSettings.SingletonId, cancellationToken);
        if (!hasGraphConnection)
        {
            return false;
        }

        var hasDomain = await db.Domains.AnyAsync(cancellationToken);
        if (!hasDomain)
        {
            return false;
        }

        var hasMailbox = await db.Mailboxes.AnyAsync(cancellationToken);
        if (!hasMailbox)
        {
            return false;
        }

        var hasRetention = await db.RetentionSettings
            .AnyAsync(r => r.Id == Core.Entities.RetentionSettings.SingletonId, cancellationToken);

        return hasRetention;
    }
}
