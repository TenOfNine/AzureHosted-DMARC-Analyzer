namespace DmarcAnalyzer.Core.Dmarc;

/// <summary>Parsed RFC 7489 aggregate ("feedback") report — a direct mapping of the XML, not yet an EF entity.</summary>
public sealed record ParsedDmarcReport(
    ParsedReportMetadata Metadata,
    ParsedPolicyPublished Policy,
    IReadOnlyList<ParsedDmarcRecord> Records);

public sealed record ParsedReportMetadata(
    string OrgName,
    string Email,
    string? ExtraContactInfo,
    string ReportId,
    DateTime DateRangeBeginUtc,
    DateTime DateRangeEndUtc);

/// <summary>Adkim/Aspf/P/Sp are the raw XML string values ("r"/"s", "none"/"quarantine"/"reject") — mapped to enums by the ingestion pipeline.</summary>
public sealed record ParsedPolicyPublished(
    string Domain,
    string Adkim,
    string Aspf,
    string P,
    string? Sp,
    int Pct,
    string? Fo);

public sealed record ParsedDmarcRecord(
    string SourceIp,
    int Count,
    string Disposition,
    string PolicyEvaluatedDkim,
    string PolicyEvaluatedSpf,
    IReadOnlyList<string>? PolicyOverrideReasons,
    string HeaderFrom,
    string? EnvelopeFrom,
    string? EnvelopeTo,
    IReadOnlyList<ParsedDkimAuthResult> DkimResults,
    IReadOnlyList<ParsedSpfAuthResult> SpfResults);

public sealed record ParsedDkimAuthResult(string Domain, string? Selector, string Result, string? HumanResult);

public sealed record ParsedSpfAuthResult(string Domain, string? Scope, string Result);
