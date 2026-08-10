using DmarcAnalyzer.Core.Abstractions;

namespace DmarcAnalyzer.Tests.TestDoubles;

public class FakeGraphMailboxClient : IGraphMailboxClient
{
    public List<GraphMessageSummary> Messages { get; } = [];
    public Dictionary<string, List<GraphAttachment>> AttachmentsByMessageId { get; } = [];
    public List<string> AttachmentRequestsMade { get; } = [];

    public Task<MailboxPollResult> ListNewMessagesAsync(string mailboxUpn, string mailFolder, string? deltaLink, CancellationToken cancellationToken = default) =>
        Task.FromResult(new MailboxPollResult(Messages, "fake-delta-link"));

    public Task<IReadOnlyList<GraphAttachment>> GetAttachmentsAsync(string mailboxUpn, string messageId, CancellationToken cancellationToken = default)
    {
        AttachmentRequestsMade.Add(messageId);
        return Task.FromResult<IReadOnlyList<GraphAttachment>>(AttachmentsByMessageId.TryGetValue(messageId, out var v) ? v : []);
    }

    public Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
}
