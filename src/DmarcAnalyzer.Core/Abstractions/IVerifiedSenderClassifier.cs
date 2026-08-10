using DmarcAnalyzer.Core.Entities;

namespace DmarcAnalyzer.Core.Abstractions;

/// <summary>
/// Decides whether a record's source IP counts as a "verified sender": DMARC-aligned pass (SPF or DKIM),
/// or an explicit admin-curated <see cref="VerifiedSenderOverride"/> match.
/// </summary>
public interface IVerifiedSenderClassifier
{
    bool IsVerifiedSender(DmarcRecord record, IReadOnlyList<VerifiedSenderOverride> overrides);
}
