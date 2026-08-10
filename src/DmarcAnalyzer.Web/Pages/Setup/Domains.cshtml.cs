using System.ComponentModel.DataAnnotations;
using DmarcAnalyzer.Core.Entities;
using DmarcAnalyzer.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DmarcAnalyzer.Web.Pages.Setup;

public class DomainsModel(DmarcAnalyzerDbContext db) : PageModel
{
    public List<Domain> Domains { get; set; } = [];

    [BindProperty]
    [Required(ErrorMessage = "Enter a domain name.")]
    [Display(Name = "Domain name")]
    public string NewDomainName { get; set; } = string.Empty;

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostAddAsync()
    {
        var normalized = NewDomainName.Trim().TrimEnd('.').ToLowerInvariant();
        if (!string.IsNullOrEmpty(normalized))
        {
            var exists = await db.Domains.AnyAsync(d => d.DomainName == normalized);
            if (!exists)
            {
                db.Domains.Add(new Domain
                {
                    Id = Guid.NewGuid(),
                    DomainName = normalized,
                    IsActive = true,
                    CreatedUtc = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRemoveAsync(Guid id)
    {
        var domain = await db.Domains.FindAsync(id);
        if (domain is not null)
        {
            db.Domains.Remove(domain);
            await db.SaveChangesAsync();
        }

        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Domains = await db.Domains.OrderBy(d => d.DomainName).ToListAsync();
    }
}
