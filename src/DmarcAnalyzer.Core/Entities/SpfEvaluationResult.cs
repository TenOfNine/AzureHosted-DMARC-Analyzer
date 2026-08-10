namespace DmarcAnalyzer.Core.Entities;

/// <summary>
/// Independently recomputed SPF verdict for a record's source IP against the domain's SPF record
/// AS RESOLVED TODAY — not simply a copy of the report's own claimed result. This is what lets the
/// dashboard flag "this report claimed SPF pass, but the domain's SPF record has since changed and
/// this sender would no longer be authorized."
/// </summary>
public class SpfEvaluationResult
{
    public Guid Id { get; set; }

    public Guid DmarcRecordId { get; set; }
    public DmarcRecord DmarcRecord { get; set; } = null!;

    public DateTime EvaluatedUtc { get; set; } = DateTime.UtcNow;
    public string? LiveSpfRecordText { get; set; }
    public SpfResultCode RecomputedResult { get; set; }
    public string? MatchedMechanism { get; set; }
    public int LookupCount { get; set; }

    /// <summary>True when RecomputedResult disagrees with the report's own SpfAuthResult.Result for this source IP.</summary>
    public bool DiscrepancyFlag { get; set; }
    public string? DiscrepancyNote { get; set; }
}
