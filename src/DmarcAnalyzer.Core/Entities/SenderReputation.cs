using System.ComponentModel.DataAnnotations.Schema;

namespace DmarcAnalyzer.Core.Entities;

/// <summary>
/// Aggregated, continuously-updated legitimacy assessment for one (domain, source IP) pair —
/// distinct from <see cref="DmarcRecord"/>/<see cref="SpfEvaluationResult"/>, which capture a
/// single report's data point. Upserted by the ingestion pipeline every time a new record is seen
/// for that source IP, combining: cumulative DMARC-aligned pass history, the most recent live SPF
/// re-check and DKIM selector staleness, reverse-DNS presence, and an admin allowlist match.
/// </summary>
public class SenderReputation
{
    public Guid Id { get; set; }

    public Guid DomainId { get; set; }
    public Domain Domain { get; set; } = null!;

    public string SourceIp { get; set; } = string.Empty;

    public DateTime FirstSeenUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }
    public long TotalVolume { get; set; }
    public long AlignedPassVolume { get; set; }

    /// <summary>The most recently recomputed live SPF result for this source IP (see <see cref="SpfEvaluationResult"/>).</summary>
    public SpfResultCode CurrentSpfResult { get; set; }

    /// <summary>Whether the most recent DKIM auth result for this source IP resolved to a stale selector (see <see cref="DkimSelectorCheck.StaleFlag"/>).</summary>
    public bool CurrentDkimStale { get; set; }

    public string? ReverseDnsHostname { get; set; }

    /// <summary>Forward-confirmed reverse DNS: the PTR hostname's own A/AAAA records include this source IP.</summary>
    public bool ForwardConfirmed { get; set; }

    public bool IsOverrideMatch { get; set; }

    public SenderLegitimacyLevel LegitimacyLevel { get; set; }

    public DateTime LastEvaluatedUtc { get; set; }

    [NotMapped]
    public double AlignedPassRatio => TotalVolume > 0 ? (double)AlignedPassVolume / TotalVolume : 0d;
}
