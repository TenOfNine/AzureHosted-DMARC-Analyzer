using DmarcAnalyzer.Core.Entities;

namespace DmarcAnalyzer.Core.Dmarc;

/// <summary>Maps the raw string enumeration values used in RFC 7489 XML to the strongly-typed Core enums.</summary>
public static class DmarcEnumMapper
{
    public static DmarcDisposition MapDisposition(string value) => value.ToLowerInvariant() switch
    {
        "none" => DmarcDisposition.None,
        "quarantine" => DmarcDisposition.Quarantine,
        "reject" => DmarcDisposition.Reject,
        _ => throw new DmarcParseException($"Unrecognized disposition value: '{value}'.")
    };

    public static DmarcPolicyResult MapPolicyResult(string value) => value.ToLowerInvariant() switch
    {
        "pass" => DmarcPolicyResult.Pass,
        "fail" => DmarcPolicyResult.Fail,
        _ => throw new DmarcParseException($"Unrecognized policy_evaluated result value: '{value}'.")
    };

    public static DmarcAlignmentMode MapAlignmentMode(string value) => value.ToLowerInvariant() switch
    {
        "r" => DmarcAlignmentMode.Relaxed,
        "s" => DmarcAlignmentMode.Strict,
        _ => throw new DmarcParseException($"Unrecognized alignment mode value: '{value}'.")
    };

    public static SpfResultCode MapSpfResult(string value) => value.ToLowerInvariant() switch
    {
        "none" => SpfResultCode.None,
        "neutral" => SpfResultCode.Neutral,
        "pass" => SpfResultCode.Pass,
        "fail" => SpfResultCode.Fail,
        "softfail" => SpfResultCode.SoftFail,
        "temperror" => SpfResultCode.TempError,
        "permerror" => SpfResultCode.PermError,
        _ => throw new DmarcParseException($"Unrecognized SPF result value: '{value}'.")
    };

    public static DkimResultCode MapDkimResult(string value) => value.ToLowerInvariant() switch
    {
        "none" => DkimResultCode.None,
        "pass" => DkimResultCode.Pass,
        "fail" => DkimResultCode.Fail,
        "policy" => DkimResultCode.Policy,
        "neutral" => DkimResultCode.Neutral,
        "temperror" => DkimResultCode.TempError,
        "permerror" => DkimResultCode.PermError,
        _ => throw new DmarcParseException($"Unrecognized DKIM result value: '{value}'.")
    };

    /// <summary>Per RFC 7489, the SPF auth_results scope defaults to "mfrom" when omitted.</summary>
    public static SpfScope MapSpfScope(string? value) => value?.ToLowerInvariant() switch
    {
        "helo" => SpfScope.Helo,
        "mfrom" or null or "" => SpfScope.MfFrom,
        _ => throw new DmarcParseException($"Unrecognized SPF scope value: '{value}'.")
    };
}
