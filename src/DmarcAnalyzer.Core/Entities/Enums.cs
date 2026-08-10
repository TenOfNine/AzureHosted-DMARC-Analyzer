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
