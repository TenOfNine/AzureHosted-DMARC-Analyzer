using DmarcAnalyzer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DmarcAnalyzer.Infrastructure.Data.Configurations;

public class RetentionSettingsConfiguration : IEntityTypeConfiguration<RetentionSettings>
{
    public void Configure(EntityTypeBuilder<RetentionSettings> builder)
    {
        builder.ToTable("RetentionSettings");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
    }
}
