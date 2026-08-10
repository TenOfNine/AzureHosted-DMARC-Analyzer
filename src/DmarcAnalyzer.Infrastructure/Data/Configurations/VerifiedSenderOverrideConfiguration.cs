using DmarcAnalyzer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DmarcAnalyzer.Infrastructure.Data.Configurations;

public class VerifiedSenderOverrideConfiguration : IEntityTypeConfiguration<VerifiedSenderOverride>
{
    public void Configure(EntityTypeBuilder<VerifiedSenderOverride> builder)
    {
        builder.ToTable("VerifiedSenderOverrides");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.SourceIpCidr).HasMaxLength(64);
        builder.Property(v => v.OrgNamePattern).HasMaxLength(255);
        builder.Property(v => v.Label).HasMaxLength(255).IsRequired();

        builder.HasOne(v => v.Domain)
            .WithMany(d => d.VerifiedSenderOverrides)
            .HasForeignKey(v => v.DomainId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
