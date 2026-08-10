using System.IO.Compression;
using System.Text;
using DmarcAnalyzer.Core.Abstractions;
using DmarcAnalyzer.Core.Dkim;
using DmarcAnalyzer.Core.Entities;
using DmarcAnalyzer.Core.Legitimacy;
using DmarcAnalyzer.Core.Spf;
using DmarcAnalyzer.Infrastructure.Data;
using DmarcAnalyzer.Infrastructure.Ingestion;
using DmarcAnalyzer.Tests.Dmarc;
using DmarcAnalyzer.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DmarcAnalyzer.Tests.Ingestion;

public class MailboxPollingServiceTests
{
    [Fact]
    public async Task PollOnceAsync_SameMessageIdOnSecondPoll_IsNotReprocessed()
    {
        var graphClient = new FakeGraphMailboxClient();
        graphClient.Messages.Add(new GraphMessageSummary("msg-1", "DMARC report", DateTimeOffset.UtcNow, true));
        graphClient.AttachmentsByMessageId["msg-1"] = [new GraphAttachment("report.xml.zip", "application/zip", ZipXml(DmarcXmlFixtures.ValidFull))];

        await using var provider = BuildServices(graphClient, nameof(PollOnceAsync_SameMessageIdOnSecondPoll_IsNotReprocessed));
        var mailboxId = await SeedDomainAndMailboxAsync(provider);
        var service = new MailboxPollingService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new IngestionOptions()),
            NullLogger<MailboxPollingService>.Instance);

        await service.PollOnceAsync(CancellationToken.None);
        await service.PollOnceAsync(CancellationToken.None);

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DmarcAnalyzerDbContext>();
        Assert.Single(db.ProcessedMessages);
        Assert.Single(db.AggregateReports);
        // Attachments are only fetched once — the second poll skips the already-processed message entirely.
        Assert.Single(graphClient.AttachmentRequestsMade);

        _ = mailboxId;
    }

    [Fact]
    public async Task PollOnceAsync_OneFailingMessage_DoesNotBlockTheRestOfTheBatch()
    {
        var graphClient = new FakeGraphMailboxClient();
        graphClient.Messages.Add(new GraphMessageSummary("bad-msg", "Broken report", DateTimeOffset.UtcNow, true));
        graphClient.Messages.Add(new GraphMessageSummary("good-msg", "Good report", DateTimeOffset.UtcNow, true));
        graphClient.AttachmentsByMessageId["bad-msg"] = [new GraphAttachment("report.xml", "text/xml", Encoding.UTF8.GetBytes(DmarcXmlFixtures.MalformedXml))];
        graphClient.AttachmentsByMessageId["good-msg"] = [new GraphAttachment("report.xml.zip", "application/zip", ZipXml(DmarcXmlFixtures.ValidFull))];

        await using var provider = BuildServices(graphClient, nameof(PollOnceAsync_OneFailingMessage_DoesNotBlockTheRestOfTheBatch));
        await SeedDomainAndMailboxAsync(provider);
        var service = new MailboxPollingService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new IngestionOptions()),
            NullLogger<MailboxPollingService>.Instance);

        await service.PollOnceAsync(CancellationToken.None);

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DmarcAnalyzerDbContext>();

        var processed = await db.ProcessedMessages.ToListAsync();
        Assert.Equal(2, processed.Count);
        Assert.Equal(MessageProcessingStatus.Failed, processed.Single(p => p.GraphMessageId == "bad-msg").Status);
        Assert.Equal(MessageProcessingStatus.Success, processed.Single(p => p.GraphMessageId == "good-msg").Status);
        Assert.Single(db.AggregateReports);
    }

    private static async Task<Guid> SeedDomainAndMailboxAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DmarcAnalyzerDbContext>();

        var domain = new Domain { Id = Guid.NewGuid(), DomainName = "contoso.com", IsActive = true, CreatedUtc = DateTime.UtcNow };
        var mailbox = new Mailbox { Id = Guid.NewGuid(), MailboxUpn = "dmarc@contoso.com", MailFolder = "inbox", IsActive = true, CreatedUtc = DateTime.UtcNow };
        mailbox.DomainMailboxes.Add(new DomainMailbox { DomainId = domain.Id, MailboxId = mailbox.Id });

        db.Domains.Add(domain);
        db.Mailboxes.Add(mailbox);
        await db.SaveChangesAsync();

        return mailbox.Id;
    }

    private static ServiceProvider BuildServices(FakeGraphMailboxClient graphClient, string dbName)
    {
        var services = new ServiceCollection();
        services.AddDbContext<DmarcAnalyzerDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddSingleton<IGraphMailboxClient>(graphClient);
        services.AddScoped<ISpfDnsResolver>(_ => new FakeSpfDnsResolver());
        services.AddScoped<IDkimDnsResolver>(_ => new FakeDkimDnsResolver());
        services.AddScoped<IReverseDnsResolver>(_ => new FakeReverseDnsResolver());
        services.AddScoped<SpfEvaluator>();
        services.AddScoped<DkimSelectorChecker>();
        services.AddScoped<IDmarcReportIngestionPipeline, DmarcReportIngestionPipeline>();
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    private static byte[] ZipXml(string xml)
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("report.xml");
            using var entryStream = entry.Open();
            using var writer = new StreamWriter(entryStream, Encoding.UTF8);
            writer.Write(xml);
        }

        return memoryStream.ToArray();
    }
}
