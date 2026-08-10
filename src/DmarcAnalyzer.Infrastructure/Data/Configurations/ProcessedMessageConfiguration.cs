using DmarcAnalyzer.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DmarcAnalyzer.Infrastructure.Data.Configurations;

public class ProcessedMessageConfiguration : IEntityTypeConfiguration<ProcessedMessage>
{
    public void Configure(EntityTypeBuilder<ProcessedMessage> builder)
    {
        builder.ToTable("ProcessedMessages");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.GraphMessageId).HasMaxLength(512).IsRequired();
        builder.Property(p => p.FailureReason).HasMaxLength(2000);

        builder.HasIndex(p => new { p.MailboxId, p.GraphMessageId }).IsUnique();

        builder.HasOne(p => p.Mailbox)
            .WithMany()
            .HasForeignKey(p => p.MailboxId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
