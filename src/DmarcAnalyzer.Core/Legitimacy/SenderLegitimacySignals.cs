using DmarcAnalyzer.Core.Entities;

namespace DmarcAnalyzer.Core.Legitimacy;

/// <summary>The pre-gathered inputs <see cref="SenderLegitimacyEvaluator"/> scores — gathering them (DNS lookups, DB aggregation) is an Infrastructure concern; this record keeps the scoring rule itself pure and independently testable.</summary>
public sealed record SenderLegitimacySignals(
    double AlignedPassRatio,
    SpfResultCode CurrentSpfResult,
    bool CurrentDkimStale,
    bool HasReverseDns,
    bool ForwardConfirmed,
    bool IsOverrideMatch);
