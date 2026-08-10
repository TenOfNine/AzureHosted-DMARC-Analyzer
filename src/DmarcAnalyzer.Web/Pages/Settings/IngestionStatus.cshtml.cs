using DmarcAnalyzer.Core.Entities;
using DmarcAnalyzer.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RetentionSettingsEntity = DmarcAnalyzer.Core.Entities.RetentionSettings;

namespace DmarcAnalyzer.Web.Pages.Settings;

public class IngestionStatusModel(DmarcAnalyzerDbContext db) : PageModel
{
    public List<Mailbox> Mailboxes { get; set; } = [];
    public List<ProcessedMessage> RecentFailures { get; set; } = [];
    public RetentionSettingsEntity? Retention { get; set; }

    public async Task OnGetAsync()
    {
        Mailboxes = await db.Mailboxes.OrderBy(m => m.MailboxUpn).ToListAsync();

        RecentFailures = await db.ProcessedMessages
            .Include(p => p.Mailbox)
            .Where(p => p.Status == MessageProcessingStatus.Failed)
            .OrderByDescending(p => p.ProcessedUtc)
            .Take(20)
            .ToListAsync();

        Retention = await db.RetentionSettings.FindAsync(RetentionSettingsEntity.SingletonId);
    }
}
