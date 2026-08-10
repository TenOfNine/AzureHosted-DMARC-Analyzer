using DmarcAnalyzer.Core.Abstractions;
using DmarcAnalyzer.Core.Entities;
using DmarcAnalyzer.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DmarcAnalyzer.Web.Pages.Dashboard;

public class DomainDetailModel(DmarcAnalyzerDbContext db, IVerifiedSenderClassifier verifiedSenderClassifier) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid DomainId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int Days { get; set; } = 30;

    [BindProperty(SupportsGet = true)]
    public bool UnverifiedOnly { get; set; }

    public Domain? Domain { get; set; }
    public List<RecordRow> Records { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        Domain = await db.Domains.FindAsync(DomainId);
        if (Domain is null)
        {
            return NotFound();
        }

        var windowDays = Math.Clamp(Days, 1, 365);
        var cutoff = DateTime.UtcNow.AddDays(-windowDays);

        var records = await db.Records
            .Include(r => r.AggregateReport)
            .Include(r => r.DkimAuthResults).ThenInclude(d => d.SelectorCheck)
            .Include(r => r.SpfEvaluation)
            .Where(r => r.AggregateReport.DomainId == DomainId && r.AggregateReport.DateRangeEndUtc >= cutoff)
            .OrderByDescending(r => r.AggregateReport.DateRangeEndUtc)
            .Take(500)
            .ToListAsync();

        var overrides = await db.VerifiedSenderOverrides.Where(v => v.DomainId == DomainId).ToListAsync();

        foreach (var record in records)
        {
            var verified = verifiedSenderClassifier.IsVerifiedSender(record, overrides);
            if (UnverifiedOnly && verified)
            {
                continue;
            }

            Records.Add(new RecordRow(
                record.AggregateReport.DateRangeEndUtc,
                record.AggregateReport.OrgName,
                record.SourceIp,
                record.Count,
                record.PolicyEvaluatedSpf,
                record.PolicyEvaluatedDkim,
                record.PolicyEvaluatedDisposition,
                verified,
                record.SpfEvaluation?.DiscrepancyFlag ?? false,
                record.DkimAuthResults.Any(d => d.SelectorCheck is { StaleFlag: true })));
        }

        return Page();
    }

    public record RecordRow(
        DateTime ReportDate,
        string OrgName,
        string SourceIp,
        int Count,
        DmarcPolicyResult SpfResult,
        DmarcPolicyResult DkimResult,
        DmarcDisposition Disposition,
        bool Verified,
        bool SpfDiscrepancy,
        bool DkimStale);
}
