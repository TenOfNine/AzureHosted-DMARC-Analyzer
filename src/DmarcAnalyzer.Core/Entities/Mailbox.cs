namespace DmarcAnalyzer.Core.Entities;

/// <summary>An Exchange Online shared mailbox polled via Microsoft Graph for DMARC reports.</summary>
public class Mailbox
{
    public Guid Id { get; set; }
    public string MailboxUpn { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string MailFolder { get; set; } = "Inbox";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastPolledUtc { get; set; }
    public MailboxPollStatus LastPollStatus { get; set; } = MailboxPollStatus.NeverPolled;
    public string? LastPollError { get; set; }

    /// <summary>Microsoft Graph delta query link, so re-polls only fetch messages new since the last run.</summary>
    public string? DeltaLink { get; set; }

    public ICollection<DomainMailbox> DomainMailboxes { get; set; } = new List<DomainMailbox>();
}
