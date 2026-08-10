using System.ComponentModel.DataAnnotations;
using DmarcAnalyzer.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RetentionSettingsEntity = DmarcAnalyzer.Core.Entities.RetentionSettings;

namespace DmarcAnalyzer.Web.Pages.Setup;

public class RetentionModel(DmarcAnalyzerDbContext db) : PageModel
{
    [BindProperty]
    [Range(30, 3650, ErrorMessage = "Retention must be between 30 and 3650 days.")]
    [Display(Name = "Retention period (days)")]
    public int RetentionDays { get; set; } = RetentionSettingsEntity.DefaultRetentionDays;

    public async Task OnGetAsync()
    {
        var existing = await db.RetentionSettings.FindAsync(RetentionSettingsEntity.SingletonId);
        if (existing is not null)
        {
            RetentionDays = existing.RetentionDays;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var settings = await db.RetentionSettings.FindAsync(RetentionSettingsEntity.SingletonId);
        var isNew = settings is null;
        settings ??= new RetentionSettingsEntity { Id = RetentionSettingsEntity.SingletonId };

        settings.RetentionDays = RetentionDays;

        if (isNew)
        {
            db.RetentionSettings.Add(settings);
        }

        await db.SaveChangesAsync();

        return RedirectToPage("Review");
    }
}
