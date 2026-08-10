using System.ComponentModel.DataAnnotations;
using DmarcAnalyzer.Core.Entities;
using DmarcAnalyzer.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DmarcAnalyzer.Web.Pages.Setup;

public class MailboxesModel(DmarcAnalyzerDbContext db) : PageModel
{
    public List<Mailbox> Mailboxes { get; set; } = [];
    public List<Domain> AvailableDomains { get; set; } = [];

    [BindProperty]
    [Required(ErrorMessage = "Enter the shared mailbox's email address.")]
    [EmailAddress]
    [Display(Name = "Shared mailbox address")]
    public string NewMailboxUpn { get; set; } = string.Empty;

    [BindProperty]
    [Display(Name = "Display name (optional)")]
    public string? NewDisplayName { get; set; }

    [BindProperty]
    [Display(Name = "Mail folder")]
    public string NewMailFolder { get; set; } = "inbox";

    [BindProperty]
    [Display(Name = "Serves domain(s)")]
    public List<Guid> SelectedDomainIds { get; set; } = [];

    public string? ValidationError { get; private set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostAddAsync()
    {
        if (!ModelState.IsValid || SelectedDomainIds.Count == 0)
        {
            ValidationError = "Select at least one domain this mailbox receives DMARC reports for.";
            await LoadAsync();
            return Page();
        }

        var mailbox = new Mailbox
        {
            Id = Guid.NewGuid(),
            MailboxUpn = NewMailboxUpn.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(NewDisplayName) ? null : NewDisplayName.Trim(),
            MailFolder = string.IsNullOrWhiteSpace(NewMailFolder) ? "inbox" : NewMailFolder.Trim().ToLowerInvariant(),
            IsActive = true,
            CreatedUtc = DateTime.UtcNow
        };

        foreach (var domainId in SelectedDomainIds)
        {
            mailbox.DomainMailboxes.Add(new DomainMailbox { DomainId = domainId, MailboxId = mailbox.Id });
        }

        db.Mailboxes.Add(mailbox);
        await db.SaveChangesAsync();

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRemoveAsync(Guid id)
    {
        var mailbox = await db.Mailboxes.FindAsync(id);
        if (mailbox is not null)
        {
            db.Mailboxes.Remove(mailbox);
            await db.SaveChangesAsync();
        }

        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Mailboxes = await db.Mailboxes
            .Include(m => m.DomainMailboxes)
            .ThenInclude(dm => dm.Domain)
            .OrderBy(m => m.MailboxUpn)
            .ToListAsync();
        AvailableDomains = await db.Domains.Where(d => d.IsActive).OrderBy(d => d.DomainName).ToListAsync();
    }
}
