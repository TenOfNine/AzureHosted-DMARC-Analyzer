using DmarcAnalyzer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DmarcAnalyzer.Infrastructure.Data.Configurations;

public class SenderReputationConfiguration : IEntityTypeConfiguration<SenderReputation>
{
    public void Configure(EntityTypeBuilder<SenderReputation> builder)
    {
        builder.ToTable("SenderReputations");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.SourceIp).HasMaxLength(45).IsRequired();
        builder.Property(s => s.ReverseDnsHostname).HasMaxLength(255);

        builder.HasIndex(s => new { s.DomainId, s.SourceIp }).IsUnique();
        builder.HasIndex(s => new { s.DomainId, s.LegitimacyLevel });

        builder.HasOne(s => s.Domain)
            .WithMany()
            .HasForeignKey(s => s.DomainId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
