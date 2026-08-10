using DmarcAnalyzer.Core.Abstractions;
using DmarcAnalyzer.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RetentionSettingsEntity = DmarcAnalyzer.Core.Entities.RetentionSettings;

namespace DmarcAnalyzer.Web.Pages.Setup;

public class ReviewModel(DmarcAnalyzerDbContext db, ISetupStateService setupStateService) : PageModel
{
    public bool IsSetupComplete { get; set; }
    public bool GraphConfigured { get; set; }
    public int DomainCount { get; set; }
    public int MailboxCount { get; set; }
    public int RetentionDays { get; set; }

    public async Task OnGetAsync()
    {
        IsSetupComplete = await setupStateService.IsSetupCompleteAsync();
        GraphConfigured = await db.GraphConnectionSettings.AnyAsync();
        DomainCount = await db.Domains.CountAsync();
        MailboxCount = await db.Mailboxes.CountAsync();
        var retention = await db.RetentionSettings.FindAsync(RetentionSettingsEntity.SingletonId);
        RetentionDays = retention?.RetentionDays ?? 0;
    }
}
