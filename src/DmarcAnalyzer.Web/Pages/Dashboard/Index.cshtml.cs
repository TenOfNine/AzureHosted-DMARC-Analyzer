using DmarcAnalyzer.Core.Entities;
using DmarcAnalyzer.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DmarcAnalyzer.Web.Pages.Dashboard;

public class IndexModel(DmarcAnalyzerDbContext db) : PageModel
{
    public List<DomainSummary> DomainSummaries { get; set; } = [];

    public async Task OnGetAsync()
    {
        var domains = await db.Domains.Where(d => d.IsActive).OrderBy(d => d.DomainName).ToListAsync();
        var cutoff = DateTime.UtcNow.AddDays(-30);

        foreach (var domain in domains)
        {
            var records = await db.Records
                .Where(r => r.AggregateReport.DomainId == domain.Id && r.AggregateReport.DateRangeEndUtc >= cutoff)
                .Select(r => new { r.Count, r.PolicyEvaluatedDkim, r.PolicyEvaluatedSpf })
                .ToListAsync();

            var totalVolume = records.Sum(r => r.Count);
            var passVolume = records
                .Where(r => r.PolicyEvaluatedDkim == DmarcPolicyResult.Pass || r.PolicyEvaluatedSpf == DmarcPolicyResult.Pass)
                .Sum(r => r.Count);
            var passRate = totalVolume > 0 ? Math.Round((double)passVolume / totalVolume * 100, 1) : (double?)null;

            var lastReportUtc = await db.AggregateReports
                .Where(r => r.DomainId == domain.Id)
                .OrderByDescending(r => r.DateRangeEndUtc)
                .Select(r => (DateTime?)r.DateRangeEndUtc)
                .FirstOrDefaultAsync();

            DomainSummaries.Add(new DomainSummary(domain.Id, domain.DomainName, totalVolume, passRate, lastReportUtc));
        }
    }

    public record DomainSummary(Guid DomainId, string DomainName, int TotalVolume30d, double? PassRatePercent, DateTime? LastReportUtc);
}
