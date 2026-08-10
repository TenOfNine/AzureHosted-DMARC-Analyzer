namespace DmarcAnalyzer.Core.Entities;

/// <summary>One record[] row of an aggregate report: a source IP plus the policy's verdict for it.</summary>
public class DmarcRecord
{
    public Guid Id { get; set; }

    public Guid AggregateReportId { get; set; }
    public DmarcAggregateReport AggregateReport { get; set; } = null!;

    public string SourceIp { get; set; } = string.Empty;
    public int Count { get; set; }

    public DmarcDisposition PolicyEvaluatedDisposition { get; set; }
    public DmarcPolicyResult PolicyEvaluatedDkim { get; set; }
    public DmarcPolicyResult PolicyEvaluatedSpf { get; set; }
    public string? PolicyOverrideReasons { get; set; }

    public string HeaderFrom { get; set; } = string.Empty;
    public string? EnvelopeFrom { get; set; }
    public string? EnvelopeTo { get; set; }

    public ICollection<DkimAuthResult> DkimAuthResults { get; set; } = new List<DkimAuthResult>();
    public ICollection<SpfAuthResult> SpfAuthResults { get; set; } = new List<SpfAuthResult>();

    /// <summary>Independently recomputed (live DNS) SPF verdict for this record's source IP against the domain's current SPF record.</summary>
    public SpfEvaluationResult? SpfEvaluation { get; set; }
}
