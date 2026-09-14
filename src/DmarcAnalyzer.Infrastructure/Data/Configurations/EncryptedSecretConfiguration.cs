using DmarcAnalyzer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DmarcAnalyzer.Infrastructure.Data.Configurations;

public class EncryptedSecretConfiguration : IEntityTypeConfiguration<EncryptedSecret>
{
    public void Configure(EntityTypeBuilder<EncryptedSecret> builder)
    {
        builder.ToTable("Secrets");
        builder.HasKey(s => s.Name);
        builder.Property(s => s.Name).HasMaxLength(255);
        builder.Property(s => s.CipherText).IsRequired();
    }
}
