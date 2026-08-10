using System.Net;
using DmarcAnalyzer.Core.Abstractions;
using DmarcAnalyzer.Core.Entities;

namespace DmarcAnalyzer.Core.Verification;

public class VerifiedSenderClassifier : IVerifiedSenderClassifier
{
    public bool IsVerifiedSender(DmarcRecord record, IReadOnlyList<VerifiedSenderOverride> overrides)
    {
        // A DMARC-aligned pass (either mechanism) is enough — this mirrors what the receiving mail
        // server itself decided, per the DMARC policy_evaluated block.
        if (record.PolicyEvaluatedDkim == DmarcPolicyResult.Pass || record.PolicyEvaluatedSpf == DmarcPolicyResult.Pass)
        {
            return true;
        }

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
