using DmarcAnalyzer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DmarcAnalyzer.Infrastructure.Data.Configurations;

public class SpfAuthResultConfiguration : IEntityTypeConfiguration<SpfAuthResult>
{
    public void Configure(EntityTypeBuilder<SpfAuthResult> builder)
    {
        builder.ToTable("SpfAuthResults");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Domain).HasMaxLength(255).IsRequired();

        builder.HasOne(s => s.DmarcRecord)
            .WithMany(r => r.SpfAuthResults)
            .HasForeignKey(s => s.DmarcRecordId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
