using DmarcAnalyzer.Core.Abstractions;
using DmarcAnalyzer.Core.Entities;

namespace DmarcAnalyzer.Infrastructure.Ingestion;

public sealed record IngestionOutcome(MessageProcessingStatus Status, AttachmentType AttachmentType, string? FailureReason);

/// <summary>
/// Turns one Graph message's attachments into persisted report data: extracts + parses the DMARC XML,
/// resolves which monitored <see cref="Domain"/> it belongs to (from the report's own
/// policy_published/domain, not from the mailbox-domain link — one shared mailbox often receives
/// reports for several domains), persists the relational rows, then runs the independent SPF/DKIM
/// re-verification for each record.
/// </summary>
public interface IDmarcReportIngestionPipeline
{
    Task<IngestionOutcome> ProcessMessageAsync(
        Mailbox mailbox,
        string graphMessageId,
        IReadOnlyList<GraphAttachment> attachments,
        CancellationToken cancellationToken = default);
}
