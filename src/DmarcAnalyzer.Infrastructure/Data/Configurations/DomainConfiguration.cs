using DmarcAnalyzer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DmarcAnalyzer.Infrastructure.Data.Configurations;

public class DomainConfiguration : IEntityTypeConfiguration<Domain>
{
    public void Configure(EntityTypeBuilder<Domain> builder)
    {
        builder.ToTable("Domains");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.DomainName).HasMaxLength(255).IsRequired();
        builder.HasIndex(d => d.DomainName).IsUnique();
    }
}
