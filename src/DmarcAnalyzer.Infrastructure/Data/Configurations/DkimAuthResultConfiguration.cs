using DmarcAnalyzer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DmarcAnalyzer.Infrastructure.Data.Configurations;

public class DkimAuthResultConfiguration : IEntityTypeConfiguration<DkimAuthResult>
{
    public void Configure(EntityTypeBuilder<DkimAuthResult> builder)
    {
        builder.ToTable("DkimAuthResults");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Domain).HasMaxLength(255).IsRequired();
        builder.Property(d => d.Selector).HasMaxLength(255);
        builder.Property(d => d.HumanResult).HasMaxLength(500);

        builder.HasOne(d => d.DmarcRecord)
            .WithMany(r => r.DkimAuthResults)
            .HasForeignKey(d => d.DmarcRecordId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.SelectorCheck)
            .WithOne(c => c.DkimAuthResult)
            .HasForeignKey<DkimSelectorCheck>(c => c.DkimAuthResultId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
