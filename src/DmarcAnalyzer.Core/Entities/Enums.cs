namespace DmarcAnalyzer.Core.Entities;

public enum DmarcDisposition
{
    None,
    Quarantine,
    Reject
}

public enum DmarcAlignmentMode
{
    Relaxed,
    Strict
}

/// <summary>policy_evaluated/dkim and policy_evaluated/spf per RFC 7489 only ever report pass/fail.</summary>
public enum DmarcPolicyResult
{
    Pass,
    Fail
}

public enum SpfScope
{
    Helo,
    MfFrom
}

public enum SpfResultCode
{
    None,
    Neutral,
    Pass,
    Fail,
    SoftFail,
    TempError,
    PermError
}

public enum DkimResultCode
{
    None,
    Pass,
    Fail,
    Policy,
    Neutral,
    TempError,
    PermError
}

public enum MessageProcessingStatus
{
    Success,
    Skipped,
    Failed
}

public enum AttachmentType
{
    Unknown,
    RuaZip,
    RuaGzip,
    RuaXml,
    Ruf
}

public enum MailboxPollStatus
{
    NeverPolled,
    Ok,
    Error
}

public enum DkimSelectorStatus
{
    Unknown,
    Valid,
    Revoked,
    Missing,
    ParseError
}

/// <summary>
/// Overall legitimacy verdict for a sending source IP, computed by
/// <see cref="Legitimacy.SenderLegitimacyEvaluator"/> from its DMARC-aligned pass history, its
/// current (live) SPF/DKIM standing, and its reverse-DNS presence — see that type for the exact
/// rules. Ordered from most to least trustworthy.
/// </summary>
public enum SenderLegitimacyLevel
{
    /// <summary>Admin-allowlisted, or a consistently DMARC-aligned pass that still holds up today.</summary>
    Verified,

    /// <summary>Some positive signal (partial aligned-pass history, a currently-passing live SPF check, or forward-confirmed reverse DNS) but not enough to fully verify.</summary>
    LikelyLegitimate,

    /// <summary>No positive signal, but nothing overtly suspicious either (e.g. has reverse DNS).</summary>
    Unverified,

    /// <summary>Not DMARC-aligned today and has no reverse DNS at all — the strongest signal this system can compute without third-party threat intelligence.</summary>
    Suspicious
}
