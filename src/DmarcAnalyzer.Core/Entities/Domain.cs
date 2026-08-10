namespace DmarcAnalyzer.Core.Entities;

/// <summary>A sender domain being monitored for DMARC compliance.</summary>
public class Domain
{
    public Guid Id { get; set; }
    public string DomainName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public ICollection<DomainMailbox> DomainMailboxes { get; set; } = new List<DomainMailbox>();
    public ICollection<DmarcAggregateReport> AggregateReports { get; set; } = new List<DmarcAggregateReport>();
    public ICollection<VerifiedSenderOverride> VerifiedSenderOverrides { get; set; } = new List<VerifiedSenderOverride>();
}
