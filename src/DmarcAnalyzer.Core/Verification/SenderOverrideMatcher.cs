using System.Net;
using DmarcAnalyzer.Core.Entities;

namespace DmarcAnalyzer.Core.Verification;

/// <summary>
/// Whether a record's source IP or reporting org matches an admin-curated
/// <see cref="VerifiedSenderOverride"/> allowlist entry — a standalone input signal feeding
/// <see cref="Legitimacy.SenderLegitimacyEvaluator"/>, independent of the record's own DMARC-aligned
/// pass/fail.
/// </summary>
public static class SenderOverrideMatcher
{
    public static bool Matches(DmarcRecord record, IReadOnlyList<VerifiedSenderOverride> overrides)
    {
        var sourceIpParsed = IPAddress.TryParse(record.SourceIp, out var sourceIp);
        var orgName = record.AggregateReport?.OrgName;

        foreach (var over in overrides)
        {
            if (sourceIpParsed
                && !string.IsNullOrEmpty(over.SourceIpCidr)
                && IPNetwork.TryParse(over.SourceIpCidr, out var network)
                && network.Contains(sourceIp!))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(over.OrgNamePattern)
                && orgName is not null
                && orgName.Contains(over.OrgNamePattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
