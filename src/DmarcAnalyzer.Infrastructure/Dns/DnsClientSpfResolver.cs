using System.Net;
using DmarcAnalyzer.Core.Spf;
using DnsClient;

namespace DmarcAnalyzer.Infrastructure.Dns;

public class DnsClientSpfResolver(ILookupClient lookupClient) : ISpfDnsResolver
{
    public async Task<IReadOnlyList<string>> GetTxtRecordsAsync(string domain, CancellationToken cancellationToken = default)
    {
        var result = await lookupClient.QueryAsync(domain, QueryType.TXT, cancellationToken: cancellationToken);
        return result.Answers.TxtRecords()
            .Select(r => string.Concat(r.Text))
            .ToList();
    }

    public async Task<IReadOnlyList<IPAddress>> ResolveAAsync(string domain, CancellationToken cancellationToken = default)
    {
        var result = await lookupClient.QueryAsync(domain, QueryType.A, cancellationToken: cancellationToken);
        return result.Answers.ARecords().Select(r => r.Address).Cast<IPAddress>().ToList();
    }

    public async Task<IReadOnlyList<IPAddress>> ResolveAaaaAsync(string domain, CancellationToken cancellationToken = default)
    {
        var result = await lookupClient.QueryAsync(domain, QueryType.AAAA, cancellationToken: cancellationToken);
        return result.Answers.AaaaRecords().Select(r => r.Address).Cast<IPAddress>().ToList();
    }

    public async Task<IReadOnlyList<string>> ResolveMxAsync(string domain, CancellationToken cancellationToken = default)
    {
        var result = await lookupClient.QueryAsync(domain, QueryType.MX, cancellationToken: cancellationToken);
        return result.Answers.MxRecords()
            .OrderBy(r => r.Preference)
            .Select(r => r.Exchange.Value.TrimEnd('.'))
            .ToList();
    }
}
