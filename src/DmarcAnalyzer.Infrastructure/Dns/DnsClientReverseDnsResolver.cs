using System.Net;
using DmarcAnalyzer.Core.Legitimacy;
using DnsClient;

namespace DmarcAnalyzer.Infrastructure.Dns;

public class DnsClientReverseDnsResolver(ILookupClient lookupClient) : IReverseDnsResolver
{
    public async Task<string?> GetPtrHostnameAsync(IPAddress ip, CancellationToken cancellationToken = default)
    {
        var result = await lookupClient.QueryReverseAsync(ip, cancellationToken);
        var ptr = result.Answers.PtrRecords().FirstOrDefault();
        return ptr?.PtrDomainName?.Value.TrimEnd('.');
    }
}
