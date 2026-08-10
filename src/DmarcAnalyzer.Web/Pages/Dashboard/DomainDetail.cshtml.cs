using DmarcAnalyzer.Core.Entities;
using DmarcAnalyzer.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DmarcAnalyzer.Web.Pages.Dashboard;

public class DomainDetailModel(DmarcAnalyzerDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid DomainId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int Days { get; set; } = 30;

    [BindProperty(SupportsGet = true)]
    public SenderLegitimacyLevel? LegitimacyFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public DmarcPolicyResult? SpfFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public DmarcPolicyResult? DkimFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public DmarcDisposition? DispositionFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    public Domain? Domain { get; set; }
    public List<RecordRow> Records { get; set; } = [];
    public List<SenderReputation> Senders { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        Domain = await db.Domains.FindAsync(DomainId);
        if (Domain is null)
        {
            return NotFound();
        }

        var windowDays = Math.Clamp(Days, 1, 365);
        var cutoff = DateTime.UtcNow.AddDays(-windowDays);
        var searchLower = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim().ToLowerInvariant();

        var reputations = await db.SenderReputations
            .Where(r => r.DomainId == DomainId)
            .ToListAsync();
        var reputationByIp = reputations.ToDictionary(r => r.SourceIp);

        Senders = reputations
            .Where(r => LegitimacyFilter is null || r.LegitimacyLevel == LegitimacyFilter)
            .Where(r => searchLower is null || r.SourceIp.Contains(searchLower) || (r.ReverseDnsHostname?.ToLowerInvariant().Contains(searchLower) ?? false))
            .OrderByDescending(r => r.TotalVolume)
            .ToList();

        var records = await db.Records
            .Include(r => r.AggregateReport)
            .Include(r => r.DkimAuthResults).ThenInclude(d => d.SelectorCheck)
            .Include(r => r.SpfEvaluation)
            .Where(r => r.AggregateReport.DomainId == DomainId && r.AggregateReport.DateRangeEndUtc >= cutoff)
            .OrderByDescending(r => r.AggregateReport.DateRangeEndUtc)
            .Take(500)
            .ToListAsync();

        foreach (var record in records)
        {
            if (SpfFilter is not null && record.PolicyEvaluatedSpf != SpfFilter)
            {
                continue;
            }

            if (DkimFilter is not null && record.PolicyEvaluatedDkim != DkimFilter)
            {
                continue;
            }

            if (DispositionFilter is not null && record.PolicyEvaluatedDisposition != DispositionFilter)
            {
                continue;
            }

            if (searchLower is not null
                && !record.SourceIp.Contains(searchLower)
                && !record.AggregateReport.OrgName.ToLowerInvariant().Contains(searchLower))
            {
                continue;
            }

            var legitimacy = reputationByIp.TryGetValue(record.SourceIp, out var reputation)
                ? reputation.LegitimacyLevel
                : SenderLegitimacyLevel.Unverified;

            if (LegitimacyFilter is not null && legitimacy != LegitimacyFilter)
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
                legitimacy,
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
        SenderLegitimacyLevel Legitimacy,
        bool SpfDiscrepancy,
        bool DkimStale);
}
