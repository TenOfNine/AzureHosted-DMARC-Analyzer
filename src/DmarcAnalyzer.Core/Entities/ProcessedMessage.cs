namespace DmarcAnalyzer.Core.Entities;

/// <summary>Dedupe ledger so a Graph message is never re-processed after a successful or terminally-failed run.</summary>
public class ProcessedMessage
{
    public Guid Id { get; set; }

    public Guid MailboxId { get; set; }
    public Mailbox Mailbox { get; set; } = null!;

    public string GraphMessageId { get; set; } = string.Empty;
    public DateTime ProcessedUtc { get; set; } = DateTime.UtcNow;
    public MessageProcessingStatus Status { get; set; }
    public AttachmentType AttachmentType { get; set; } = AttachmentType.Unknown;
    public string? FailureReason { get; set; }
}
