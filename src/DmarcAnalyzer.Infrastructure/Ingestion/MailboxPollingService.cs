using DmarcAnalyzer.Core.Abstractions;
using DmarcAnalyzer.Core.Entities;
using DmarcAnalyzer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DmarcAnalyzer.Infrastructure.Ingestion;

/// <summary>
/// Periodically polls every active mailbox via Graph delta query, extracts + ingests any new DMARC
/// report attachments, and records dedupe/status per message. Runs in-process inside the single
/// Always-On App Service — see the architecture notes in the repo plan for why that's preferred over a
/// separate Azure Function for this workload.
/// </summary>
public class MailboxPollingService(
    IServiceScopeFactory scopeFactory,
    IOptions<IngestionOptions> options,
    ILogger<MailboxPollingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(1, options.Value.PollIntervalMinutes));
        using var timer = new PeriodicTimer(interval);

        do
        {
            try
            {
                await PollAllMailboxesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Mailbox polling cycle failed unexpectedly");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PollAllMailboxesAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmarcAnalyzerDbContext>();
        var graphClient = scope.ServiceProvider.GetRequiredService<IGraphMailboxClient>();
        var pipeline = scope.ServiceProvider.GetRequiredService<IDmarcReportIngestionPipeline>();

        var mailboxes = await db.Mailboxes.Where(m => m.IsActive).ToListAsync(cancellationToken);

        foreach (var mailbox in mailboxes)
        {
            await PollMailboxAsync(db, graphClient, pipeline, mailbox, cancellationToken);
        }
    }

    private async Task PollMailboxAsync(
        DmarcAnalyzerDbContext db,
        IGraphMailboxClient graphClient,
        IDmarcReportIngestionPipeline pipeline,
        Mailbox mailbox,
        CancellationToken cancellationToken)
    {
        try
        {
            var pollResult = await graphClient.ListNewMessagesAsync(mailbox.MailboxUpn, mailbox.MailFolder, mailbox.DeltaLink, cancellationToken);

            foreach (var message in pollResult.Messages)
            {
                if (!message.HasAttachments)
                {
                    continue;
                }

                var alreadyProcessed = await db.ProcessedMessages
                    .AnyAsync(p => p.MailboxId == mailbox.Id && p.GraphMessageId == message.Id, cancellationToken);
                if (alreadyProcessed)
                {
                    continue;
                }

                await ProcessSingleMessageAsync(db, graphClient, pipeline, mailbox, message.Id, cancellationToken);
            }

            mailbox.DeltaLink = pollResult.NewDeltaLink ?? mailbox.DeltaLink;
            mailbox.LastPolledUtc = DateTime.UtcNow;
            mailbox.LastPollStatus = MailboxPollStatus.Ok;
            mailbox.LastPollError = null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to poll mailbox {Mailbox}", mailbox.MailboxUpn);
            mailbox.LastPolledUtc = DateTime.UtcNow;
            mailbox.LastPollStatus = MailboxPollStatus.Error;
            mailbox.LastPollError = Truncate(ex.Message, 2000);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// A failure processing one message (parse error, transient Graph/DNS hiccup) must not abort the
    /// rest of the mailbox's batch — it's recorded as a Failed ProcessedMessage and the loop continues.
    /// </summary>
    private async Task ProcessSingleMessageAsync(
        DmarcAnalyzerDbContext db,
        IGraphMailboxClient graphClient,
        IDmarcReportIngestionPipeline pipeline,
        Mailbox mailbox,
        string messageId,
        CancellationToken cancellationToken)
    {
        IngestionOutcome outcome;
        try
        {
            var attachments = await graphClient.GetAttachmentsAsync(mailbox.MailboxUpn, messageId, cancellationToken);
            outcome = await pipeline.ProcessMessageAsync(mailbox, messageId, attachments, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to process message {MessageId} in mailbox {Mailbox}", messageId, mailbox.MailboxUpn);
            outcome = new IngestionOutcome(MessageProcessingStatus.Failed, AttachmentType.Unknown, Truncate(ex.Message, 2000));
        }

        db.ProcessedMessages.Add(new ProcessedMessage
        {
            Id = Guid.NewGuid(),
            MailboxId = mailbox.Id,
            GraphMessageId = messageId,
            ProcessedUtc = DateTime.UtcNow,
            Status = outcome.Status,
            AttachmentType = outcome.AttachmentType,
            FailureReason = outcome.FailureReason
        });
    }

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
}
