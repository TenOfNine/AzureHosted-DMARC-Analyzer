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
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DmarcAnalyzer.Tests.Ingestion;

/// <summary>
/// Exercises the (domain, source IP) reputation upsert directly through
/// <see cref="DmarcReportIngestionPipeline"/>, since it's a persistence-heavy aggregation that the
/// pure <see cref="SenderLegitimacyEvaluatorTests"/> can't cover on its own.
/// </summary>
public class SenderReputationAggregationTests
{
    private static readonly string ValidDkimKey = $"v=DKIM1; k=rsa; p={Convert.ToBase64String(new byte[32])}";

    [Fact]
    public async Task ProcessMessageAsync_NewSourceIp_CreatesReputationWithCorrectVolumeAndRatio()
    {
        var db = await BuildDbAsync(nameof(ProcessMessageAsync_NewSourceIp_CreatesReputationWithCorrectVolumeAndRatio));
        var domain = await SeedDomainAsync(db, "contoso.com");
        var mailbox = SeedMailbox(db);

        var pipeline = BuildPipeline(db, new FakeSpfDnsResolver(), new FakeDkimDnsResolver(), new FakeReverseDnsResolver());

        await pipeline.ProcessMessageAsync(mailbox, "msg-1", [Zip(DmarcXmlFixtures.ValidFull)], CancellationToken.None);

        var passReputation = await db.SenderReputations.SingleAsync(r => r.SourceIp == "203.0.113.5" && r.DomainId == domain.Id);
        Assert.Equal(3, passReputation.TotalVolume);
        Assert.Equal(3, passReputation.AlignedPassVolume);
        Assert.Equal(1.0, passReputation.AlignedPassRatio);

        var failReputation = await db.SenderReputations.SingleAsync(r => r.SourceIp == "198.51.100.9" && r.DomainId == domain.Id);
        Assert.Equal(1, failReputation.TotalVolume);
        Assert.Equal(0, failReputation.AlignedPassVolume);
    }

    [Fact]
    public async Task ProcessMessageAsync_SameSourceIpAcrossTwoMessages_AccumulatesVolume()
    {
        var db = await BuildDbAsync(nameof(ProcessMessageAsync_SameSourceIpAcrossTwoMessages_AccumulatesVolume));
        await SeedDomainAsync(db, "contoso.com");
        var mailbox = SeedMailbox(db);

        var pipeline = BuildPipeline(db, new FakeSpfDnsResolver(), new FakeDkimDnsResolver(), new FakeReverseDnsResolver());

        await pipeline.ProcessMessageAsync(mailbox, "msg-1", [Zip(DmarcXmlFixtures.ValidFull)], CancellationToken.None);
        await pipeline.ProcessMessageAsync(mailbox, "msg-2", [Zip(DmarcXmlFixtures.ValidFull)], CancellationToken.None);

        var reputation = await db.SenderReputations.SingleAsync(r => r.SourceIp == "203.0.113.5");
        Assert.Equal(6, reputation.TotalVolume);
        Assert.Equal(6, reputation.AlignedPassVolume);
    }

    [Fact]
    public async Task ProcessMessageAsync_ConsistentlyPassingSenderWithValidDkim_IsScoredVerified()
    {
        var db = await BuildDbAsync(nameof(ProcessMessageAsync_ConsistentlyPassingSenderWithValidDkim_IsScoredVerified));
        await SeedDomainAsync(db, "contoso.com");
        var mailbox = SeedMailbox(db);

        var spfResolver = new FakeSpfDnsResolver().WithTxt("contoso.com", "v=spf1 ip4:203.0.113.0/24 -all");
        var dkimResolver = new FakeDkimDnsResolver().WithSelector("selector1", "contoso.com", ValidDkimKey);
        var pipeline = BuildPipeline(db, spfResolver, dkimResolver, new FakeReverseDnsResolver());

        await pipeline.ProcessMessageAsync(mailbox, "msg-1", [Zip(DmarcXmlFixtures.ValidFull)], CancellationToken.None);

        var reputation = await db.SenderReputations.SingleAsync(r => r.SourceIp == "203.0.113.5");
        Assert.Equal(SpfResultCode.Pass, reputation.CurrentSpfResult);
        Assert.False(reputation.CurrentDkimStale);
        Assert.Equal(SenderLegitimacyLevel.Verified, reputation.LegitimacyLevel);
    }

    [Fact]
    public async Task ProcessMessageAsync_FailingSenderWithNoReverseDns_IsScoredSuspicious()
    {
        var db = await BuildDbAsync(nameof(ProcessMessageAsync_FailingSenderWithNoReverseDns_IsScoredSuspicious));
        await SeedDomainAsync(db, "contoso.com");
        var mailbox = SeedMailbox(db);

        var spfResolver = new FakeSpfDnsResolver().WithTxt("contoso.com", "v=spf1 ip4:203.0.113.0/24 -all");
        var pipeline = BuildPipeline(db, spfResolver, new FakeDkimDnsResolver(), new FakeReverseDnsResolver());

        await pipeline.ProcessMessageAsync(mailbox, "msg-1", [Zip(DmarcXmlFixtures.ValidFull)], CancellationToken.None);

        // 198.51.100.9 is outside the SPF-authorized range, reported as fail/fail, and has no PTR configured.
        var reputation = await db.SenderReputations.SingleAsync(r => r.SourceIp == "198.51.100.9");
        Assert.Equal(SenderLegitimacyLevel.Suspicious, reputation.LegitimacyLevel);
        Assert.Null(reputation.ReverseDnsHostname);
    }

    [Fact]
    public async Task ProcessMessageAsync_FailingSenderWithForwardConfirmedReverseDns_IsUpgradedFromSuspiciousToLikelyLegitimate()
    {
        var db = await BuildDbAsync(nameof(ProcessMessageAsync_FailingSenderWithForwardConfirmedReverseDns_IsUpgradedFromSuspiciousToLikelyLegitimate));
        await SeedDomainAsync(db, "contoso.com");
        var mailbox = SeedMailbox(db);

        var spfResolver = new FakeSpfDnsResolver()
            .WithTxt("contoso.com", "v=spf1 ip4:203.0.113.0/24 -all")
            .WithA("mail.example-sender.com", "198.51.100.9");
        var reverseResolver = new FakeReverseDnsResolver().WithPtr("198.51.100.9", "mail.example-sender.com");
        var pipeline = BuildPipeline(db, spfResolver, new FakeDkimDnsResolver(), reverseResolver);

        await pipeline.ProcessMessageAsync(mailbox, "msg-1", [Zip(DmarcXmlFixtures.ValidFull)], CancellationToken.None);

        var reputation = await db.SenderReputations.SingleAsync(r => r.SourceIp == "198.51.100.9");
        Assert.Equal("mail.example-sender.com", reputation.ReverseDnsHostname);
        Assert.True(reputation.ForwardConfirmed);
        Assert.Equal(SenderLegitimacyLevel.LikelyLegitimate, reputation.LegitimacyLevel);
    }

    [Fact]
    public async Task ProcessMessageAsync_OverrideMatch_IsScoredVerifiedRegardlessOfSpfResult()
    {
        var db = await BuildDbAsync(nameof(ProcessMessageAsync_OverrideMatch_IsScoredVerifiedRegardlessOfSpfResult));
        var domain = await SeedDomainAsync(db, "contoso.com");
        var mailbox = SeedMailbox(db);

        db.VerifiedSenderOverrides.Add(new VerifiedSenderOverride
        {
            Id = Guid.NewGuid(),
            DomainId = domain.Id,
            SourceIpCidr = "198.51.100.0/24",
            Label = "Known ESP"
        });
        await db.SaveChangesAsync();

        var pipeline = BuildPipeline(db, new FakeSpfDnsResolver(), new FakeDkimDnsResolver(), new FakeReverseDnsResolver());

        await pipeline.ProcessMessageAsync(mailbox, "msg-1", [Zip(DmarcXmlFixtures.ValidFull)], CancellationToken.None);

        var reputation = await db.SenderReputations.SingleAsync(r => r.SourceIp == "198.51.100.9");
        Assert.True(reputation.IsOverrideMatch);
        Assert.Equal(SenderLegitimacyLevel.Verified, reputation.LegitimacyLevel);
    }

    private static async Task<DmarcAnalyzerDbContext> BuildDbAsync(string dbName)
    {
        var options = new DbContextOptionsBuilder<DmarcAnalyzerDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var db = new DmarcAnalyzerDbContext(options);
        await Task.CompletedTask;
        return db;
    }

    private static async Task<Domain> SeedDomainAsync(DmarcAnalyzerDbContext db, string domainName)
    {
        var domain = new Domain { Id = Guid.NewGuid(), DomainName = domainName, IsActive = true, CreatedUtc = DateTime.UtcNow };
        db.Domains.Add(domain);
        await db.SaveChangesAsync();
        return domain;
    }

    private static Mailbox SeedMailbox(DmarcAnalyzerDbContext db)
    {
        var mailbox = new Mailbox { Id = Guid.NewGuid(), MailboxUpn = "dmarc@contoso.com", MailFolder = "inbox", IsActive = true, CreatedUtc = DateTime.UtcNow };
        db.Mailboxes.Add(mailbox);
        db.SaveChanges();
        return mailbox;
    }

    private static DmarcReportIngestionPipeline BuildPipeline(
        DmarcAnalyzerDbContext db,
        FakeSpfDnsResolver spfResolver,
        FakeDkimDnsResolver dkimResolver,
        FakeReverseDnsResolver reverseResolver) =>
        new(db, new SpfEvaluator(spfResolver), new DkimSelectorChecker(dkimResolver), spfResolver, reverseResolver, NullLogger<DmarcReportIngestionPipeline>.Instance);

    private static GraphAttachment Zip(string xml) =>
        new("report.xml.zip", "application/zip", ZipXml(xml));

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
