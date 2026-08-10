using DmarcAnalyzer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DmarcAnalyzer.Infrastructure.Data.Configurations;

public class DomainMailboxConfiguration : IEntityTypeConfiguration<DomainMailbox>
{
    public void Configure(EntityTypeBuilder<DomainMailbox> builder)
    {
        builder.ToTable("DomainMailboxes");
        builder.HasKey(dm => new { dm.DomainId, dm.MailboxId });

        builder.HasOne(dm => dm.Domain)
            .WithMany(d => d.DomainMailboxes)
            .HasForeignKey(dm => dm.DomainId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(dm => dm.Mailbox)
            .WithMany(m => m.DomainMailboxes)
            .HasForeignKey(dm => dm.MailboxId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
