namespace DmarcAnalyzer.Core.Abstractions;

/// <summary>Microsoft Graph mailbox access abstraction, so ingestion logic is unit testable without real Graph calls.</summary>
public interface IGraphMailboxClient
{
    /// <summary>
    /// Lists messages new since the last poll. Pass the mailbox's stored delta link (if any) to resume;
    /// the returned <see cref="MailboxPollResult.NewDeltaLink"/> should be persisted for the next call.
    /// </summary>
    Task<MailboxPollResult> ListNewMessagesAsync(string mailboxUpn, string mailFolder, string? deltaLink, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GraphAttachment>> GetAttachmentsAsync(string mailboxUpn, string messageId, CancellationToken cancellationToken = default);

    /// <summary>Performs a lightweight authenticated call to validate the configured app registration can reach Graph.</summary>
    Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default);
}

public sealed record GraphMessageSummary(string Id, string Subject, DateTimeOffset ReceivedDateTime, bool HasAttachments);

public sealed record GraphAttachment(string Name, string? ContentType, byte[] ContentBytes);

public sealed record MailboxPollResult(IReadOnlyList<GraphMessageSummary> Messages, string? NewDeltaLink);
