using DmarcAnalyzer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DmarcAnalyzer.Infrastructure.Data.Configurations;

public class GraphConnectionSettingsConfiguration : IEntityTypeConfiguration<GraphConnectionSettings>
{
    public void Configure(EntityTypeBuilder<GraphConnectionSettings> builder)
    {
        builder.ToTable("GraphConnectionSettings");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).ValueGeneratedNever();
        builder.Property(g => g.ClientSecretKeyVaultName).HasMaxLength(255).IsRequired();
        builder.Property(g => g.LastValidationError).HasMaxLength(2000);
    }
}
