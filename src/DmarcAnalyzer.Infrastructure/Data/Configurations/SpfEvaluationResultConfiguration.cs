using DmarcAnalyzer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DmarcAnalyzer.Infrastructure.Data.Configurations;

public class SpfEvaluationResultConfiguration : IEntityTypeConfiguration<SpfEvaluationResult>
{
    public void Configure(EntityTypeBuilder<SpfEvaluationResult> builder)
    {
        builder.ToTable("SpfEvaluationResults");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.LiveSpfRecordText).HasMaxLength(2000);
        builder.Property(e => e.MatchedMechanism).HasMaxLength(500);
        builder.Property(e => e.DiscrepancyNote).HasMaxLength(1000);
    }
}
