namespace DmarcAnalyzer.Core.Entities;

/// <summary>
/// Admin-curated allowlist entry (e.g. a known ESP like SendGrid/Mailchimp) that counts toward the
/// "verified sender" badge independently of SPF/DKIM alignment math.
/// </summary>
public class VerifiedSenderOverride
{
    public Guid Id { get; set; }

    public Guid DomainId { get; set; }
    public Domain Domain { get; set; } = null!;

    /// <summary>CIDR to match against a record's source IP, e.g. "198.51.100.0/24". Optional if OrgNamePattern is set.</summary>
    public string? SourceIpCidr { get; set; }

    /// <summary>Substring/pattern to match against the reporting org name. Optional if SourceIpCidr is set.</summary>
    public string? OrgNamePattern { get; set; }

    public string Label { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
