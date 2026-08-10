using DmarcAnalyzer.Core.Entities;

namespace DmarcAnalyzer.Core.Legitimacy;

/// <summary>
/// Scores a sending source IP's overall legitimacy from independently-gathered signals — an
/// explainable heuristic, not a third-party reputation/threat-intelligence lookup (this system has
/// no access to one). Every rule here is deliberately conservative and documented, because a wrong
/// "Verified" is worse than a wrong "Unverified": an administrator investigating flagged senders is
/// the safety net, and false negatives there are cheap while false positives erode trust in the
/// tool.
///
/// Rules, evaluated in order:
/// 1. <b>Verified</b> — an explicit admin allowlist match, OR a consistently DMARC-aligned pass
///    history (&gt;= 95%) that still holds up on today's live SPF re-check with no DKIM staleness.
/// 2. <b>LikelyLegitimate</b> — some positive signal, but not enough for full verification: partial
///    aligned-pass history (&gt;= 50%), a currently-passing live SPF check, or forward-confirmed
///    reverse DNS (the PTR hostname's own A/AAAA records include this IP).
/// 3. <b>Suspicious</b> — none of the above, AND no reverse DNS (PTR record) at all. Legitimate
///    mail infrastructure overwhelmingly has a PTR record; its complete absence combined with no
///    DMARC alignment is the strongest signal this system can compute without external
///    threat-intelligence data.
/// 4. <b>Unverified</b> — none of the above, but a PTR record exists — most plausibly a legitimate
///    sender that simply isn't yet authorized in the domain's SPF/DKIM configuration.
/// </summary>
public static class SenderLegitimacyEvaluator
{
    public const double VerifiedAlignedPassRatioThreshold = 0.95;
    public const double LikelyLegitimateAlignedPassRatioThreshold = 0.5;

    public static SenderLegitimacyLevel Evaluate(SenderLegitimacySignals signals)
    {
        var spfCurrentlyPasses = signals.CurrentSpfResult == SpfResultCode.Pass;

        if (signals.IsOverrideMatch)
        {
            return SenderLegitimacyLevel.Verified;
        }

        if (signals.AlignedPassRatio >= VerifiedAlignedPassRatioThreshold && spfCurrentlyPasses && !signals.CurrentDkimStale)
        {
            return SenderLegitimacyLevel.Verified;
        }

        if (signals.AlignedPassRatio >= LikelyLegitimateAlignedPassRatioThreshold || spfCurrentlyPasses || signals.ForwardConfirmed)
        {
            return SenderLegitimacyLevel.LikelyLegitimate;
        }

        if (!signals.HasReverseDns)
        {
            return SenderLegitimacyLevel.Suspicious;
        }

        return SenderLegitimacyLevel.Unverified;
    }
}
