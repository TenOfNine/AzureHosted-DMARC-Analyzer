using DmarcAnalyzer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DmarcAnalyzer.Infrastructure.Data.Configurations;

public class DmarcRecordConfiguration : IEntityTypeConfiguration<DmarcRecord>
{
    public void Configure(EntityTypeBuilder<DmarcRecord> builder)
    {
        builder.ToTable("Records");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.SourceIp).HasMaxLength(45).IsRequired();
        builder.Property(r => r.PolicyOverrideReasons).HasMaxLength(1000);
        builder.Property(r => r.HeaderFrom).HasMaxLength(255).IsRequired();
        builder.Property(r => r.EnvelopeFrom).HasMaxLength(255);
        builder.Property(r => r.EnvelopeTo).HasMaxLength(255);

        builder.HasIndex(r => r.AggregateReportId);
        builder.HasIndex(r => r.SourceIp);

        builder.HasOne(r => r.AggregateReport)
            .WithMany(a => a.Records)
            .HasForeignKey(r => r.AggregateReportId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.SpfEvaluation)
            .WithOne(e => e.DmarcRecord)
            .HasForeignKey<SpfEvaluationResult>(e => e.DmarcRecordId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
