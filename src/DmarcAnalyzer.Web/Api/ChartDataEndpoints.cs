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

            var raw = await db.Records
                .Where(r => r.AggregateReport.DomainId == domainId && r.AggregateReport.DateRangeEndUtc >= cutoff)
                .Select(r => new
                {
                    Date = r.AggregateReport.DateRangeEndUtc.Date,
                    r.Count,
                    Passed = r.PolicyEvaluatedDkim == DmarcPolicyResult.Pass || r.PolicyEvaluatedSpf == DmarcPolicyResult.Pass
                })
                .ToListAsync();

            var byDate = raw
                .GroupBy(r => r.Date)
                .Select(g => new
                {
                    date = g.Key.ToString("yyyy-MM-dd"),
                    pass = g.Where(x => x.Passed).Sum(x => x.Count),
                    fail = g.Where(x => !x.Passed).Sum(x => x.Count)
                })
                .OrderBy(x => x.date)
                .ToList();

            return Results.Ok(byDate);
        });
    }
}
