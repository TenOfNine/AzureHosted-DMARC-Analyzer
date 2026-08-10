namespace DmarcAnalyzer.Core.Entities;

/// <summary>
/// Many-to-many link: one shared mailbox commonly receives reports for several domains,
/// and a domain's reports may land in more than one shared mailbox.
/// </summary>
public class DomainMailbox
{
    public Guid DomainId { get; set; }
    public Domain Domain { get; set; } = null!;

    public Guid MailboxId { get; set; }
    public Mailbox Mailbox { get; set; } = null!;
}
