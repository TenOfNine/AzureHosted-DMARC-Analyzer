using DmarcAnalyzer.Core.Dkim;

namespace DmarcAnalyzer.Tests.TestDoubles;

public class FakeDkimDnsResolver : IDkimDnsResolver
{
    private readonly Dictionary<string, List<string>> _records = new(StringComparer.OrdinalIgnoreCase);

    public FakeDkimDnsResolver WithSelector(string selector, string domain, params string[] txtRecords)
    {
        _records[$"{selector}._domainkey.{domain}"] = txtRecords.ToList();
        return this;
    }

    public Task<IReadOnlyList<string>> GetSelectorTxtRecordsAsync(string selector, string domain, CancellationToken cancellationToken = default)
    {
        var key = $"{selector}._domainkey.{domain}";
        return Task.FromResult<IReadOnlyList<string>>(_records.TryGetValue(key, out var v) ? v : []);
    }
}
