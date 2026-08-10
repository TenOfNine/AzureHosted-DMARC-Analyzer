using DmarcAnalyzer.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace DmarcAnalyzer.Infrastructure.Data;

public class DmarcAnalyzerDbContext(DbContextOptions<DmarcAnalyzerDbContext> options) : DbContext(options)
{
    public DbSet<Domain> Domains => Set<Domain>();
    public DbSet<Mailbox> Mailboxes => Set<Mailbox>();
    public DbSet<DomainMailbox> DomainMailboxes => Set<DomainMailbox>();
    public DbSet<GraphConnectionSettings> GraphConnectionSettings => Set<GraphConnectionSettings>();
    public DbSet<RetentionSettings> RetentionSettings => Set<RetentionSettings>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();
    public DbSet<DmarcAggregateReport> AggregateReports => Set<DmarcAggregateReport>();
    public DbSet<DmarcRecord> Records => Set<DmarcRecord>();
    public DbSet<DkimAuthResult> DkimAuthResults => Set<DkimAuthResult>();
    public DbSet<SpfAuthResult> SpfAuthResults => Set<SpfAuthResult>();
    public DbSet<SpfEvaluationResult> SpfEvaluationResults => Set<SpfEvaluationResult>();
    public DbSet<DkimSelectorCheck> DkimSelectorChecks => Set<DkimSelectorCheck>();
    public DbSet<VerifiedSenderOverride> VerifiedSenderOverrides => Set<VerifiedSenderOverride>();
    public DbSet<SenderReputation> SenderReputations => Set<SenderReputation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DmarcAnalyzerDbContext).Assembly);
    }
}
