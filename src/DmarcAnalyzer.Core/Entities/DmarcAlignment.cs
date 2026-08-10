namespace DmarcAnalyzer.Core.Entities;

/// <summary>
/// Single source of truth for the DMARC "aligned pass" rule (RFC 7489 §3): a record counts as an
/// aligned pass if either policy_evaluated mechanism reports Pass. Takes the two enum values
/// directly (rather than a <see cref="DmarcRecord"/>) so it works equally against a materialized
/// entity and against an EF projection's anonymous type.
/// </summary>
public static class DmarcAlignment
{
    public static bool IsAlignedPass(DmarcPolicyResult dkimResult, DmarcPolicyResult spfResult) =>
        dkimResult == DmarcPolicyResult.Pass || spfResult == DmarcPolicyResult.Pass;
}
