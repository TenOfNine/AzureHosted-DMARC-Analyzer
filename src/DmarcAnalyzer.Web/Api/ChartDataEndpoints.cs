using DmarcAnalyzer.Core.Entities;
using DmarcAnalyzer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DmarcAnalyzer.Web.Api;

public static class ChartDataEndpoints
{
    public static void MapChartDataEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/domains/{domainId:guid}/trend", async (Guid domainId, int? days, DmarcAnalyzerDbContext db) =>
        {
            var windowDays = Math.Clamp(days is null or <= 0 ? 30 : days.Value, 1, 365);
            var cutoff = DateTime.UtcNow.Date.AddDays(-windowDays);

            // Project the raw enum columns (translatable to SQL) rather than the derived "aligned pass"
            // boolean, so the alignment rule itself is computed once client-side via DmarcAlignment,
            // the same helper the ingestion pipeline and dashboard summary use.
            var raw = await db.Records
                .Where(r => r.AggregateReport.DomainId == domainId && r.AggregateReport.DateRangeEndUtc >= cutoff)
                .Select(r => new
                {
                    Date = r.AggregateReport.DateRangeEndUtc.Date,
                    r.Count,
                    r.PolicyEvaluatedDkim,
                    r.PolicyEvaluatedSpf
                })
                .ToListAsync();

            var byDate = raw
                .GroupBy(r => r.Date)
                .Select(g => new
                {
                    date = g.Key.ToString("yyyy-MM-dd"),
                    pass = g.Where(x => DmarcAlignment.IsAlignedPass(x.PolicyEvaluatedDkim, x.PolicyEvaluatedSpf)).Sum(x => x.Count),
                    fail = g.Where(x => !DmarcAlignment.IsAlignedPass(x.PolicyEvaluatedDkim, x.PolicyEvaluatedSpf)).Sum(x => x.Count)
                })
                .OrderBy(x => x.date)
                .ToList();

            return Results.Ok(byDate);
        });
    }
}
