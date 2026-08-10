namespace DmarcAnalyzer.Core.Entities;

/// <summary>One RUA (aggregate) DMARC report, mapped from an RFC 7489 feedback/report_metadata + policy_published block.</summary>
public class DmarcAggregateReport
{
    public Guid Id { get; set; }

    public Guid DomainId { get; set; }
    public Domain Domain { get; set; } = null!;

    public Guid MailboxId { get; set; }
    public Mailbox Mailbox { get; set; } = null!;

    public string GraphMessageId { get; set; } = string.Empty;
    public string ReportId { get; set; } = string.Empty;
    public string OrgName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? ExtraContactInfo { get; set; }
    public DateTime DateRangeBeginUtc { get; set; }
    public DateTime DateRangeEndUtc { get; set; }

    public string PolicyPublishedDomain { get; set; } = string.Empty;
    public DmarcAlignmentMode PolicyAdkim { get; set; }
    public DmarcAlignmentMode PolicyAspf { get; set; }
    public DmarcDisposition PolicyP { get; set; }
    public DmarcDisposition? PolicySp { get; set; }
    public int PolicyPct { get; set; } = 100;
    public string? PolicyFo { get; set; }

    public DateTime ReceivedUtc { get; set; } = DateTime.UtcNow;

    public ICollection<DmarcRecord> Records { get; set; } = new List<DmarcRecord>();
}
