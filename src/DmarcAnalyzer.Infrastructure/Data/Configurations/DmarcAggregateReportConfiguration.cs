using DmarcAnalyzer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DmarcAnalyzer.Infrastructure.Data.Configurations;

public class DmarcAggregateReportConfiguration : IEntityTypeConfiguration<DmarcAggregateReport>
{
    public void Configure(EntityTypeBuilder<DmarcAggregateReport> builder)
    {
        builder.ToTable("AggregateReports");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.GraphMessageId).HasMaxLength(512).IsRequired();
        builder.Property(r => r.ReportId).HasMaxLength(255).IsRequired();
        builder.Property(r => r.OrgName).HasMaxLength(255).IsRequired();
        builder.Property(r => r.Email).HasMaxLength(320).IsRequired();
        builder.Property(r => r.ExtraContactInfo).HasMaxLength(320);
        builder.Property(r => r.PolicyPublishedDomain).HasMaxLength(255).IsRequired();
        builder.Property(r => r.PolicyFo).HasMaxLength(50);

        builder.HasIndex(r => new { r.DomainId, r.DateRangeBeginUtc });

        builder.HasOne(r => r.Domain)
            .WithMany(d => d.AggregateReports)
            .HasForeignKey(r => r.DomainId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict (not cascade) so a mailbox can be removed from Settings without needing to
        // decide what happens to reports it already ingested — deactivate it instead (Mailbox.IsActive).
        builder.HasOne(r => r.Mailbox)
            .WithMany()
            .HasForeignKey(r => r.MailboxId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
