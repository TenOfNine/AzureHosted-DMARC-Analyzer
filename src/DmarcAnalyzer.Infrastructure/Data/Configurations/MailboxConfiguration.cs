using DmarcAnalyzer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DmarcAnalyzer.Infrastructure.Data.Configurations;

public class MailboxConfiguration : IEntityTypeConfiguration<Mailbox>
{
    public void Configure(EntityTypeBuilder<Mailbox> builder)
    {
        builder.ToTable("Mailboxes");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.MailboxUpn).HasMaxLength(320).IsRequired();
        builder.Property(m => m.MailFolder).HasMaxLength(255).IsRequired();
        builder.Property(m => m.LastPollError).HasMaxLength(2000);
        builder.HasIndex(m => m.MailboxUpn).IsUnique();
    }
}
