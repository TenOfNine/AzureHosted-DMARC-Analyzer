using DmarcAnalyzer.Core.Dkim;
using DnsClient;

namespace DmarcAnalyzer.Infrastructure.Dns;

public class DnsClientDkimResolver(ILookupClient lookupClient) : IDkimDnsResolver
{
    public async Task<IReadOnlyList<string>> GetSelectorTxtRecordsAsync(string selector, string domain, CancellationToken cancellationToken = default)
    {
        var query = $"{selector}._domainkey.{domain}";
        var result = await lookupClient.QueryAsync(query, QueryType.TXT, cancellationToken: cancellationToken);
        return result.Answers.TxtRecords()
            .Select(r => string.Concat(r.Text))
            .ToList();
    }
}
