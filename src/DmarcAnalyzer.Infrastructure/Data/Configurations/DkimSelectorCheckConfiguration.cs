using DmarcAnalyzer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DmarcAnalyzer.Infrastructure.Data.Configurations;

public class DkimSelectorCheckConfiguration : IEntityTypeConfiguration<DkimSelectorCheck>
{
    public void Configure(EntityTypeBuilder<DkimSelectorCheck> builder)
    {
        builder.ToTable("DkimSelectorChecks");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.RawTxtRecord).HasMaxLength(4000);
        builder.HasIndex(c => c.DkimAuthResultId).IsUnique();
    }
}
