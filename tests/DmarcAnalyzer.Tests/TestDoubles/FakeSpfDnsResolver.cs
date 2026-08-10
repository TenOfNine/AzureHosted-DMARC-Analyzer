using System.Net;
using DmarcAnalyzer.Core.Spf;

namespace DmarcAnalyzer.Tests.TestDoubles;

public class FakeSpfDnsResolver : ISpfDnsResolver
{
    private readonly Dictionary<string, List<string>> _txt = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<IPAddress>> _a = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<IPAddress>> _aaaa = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _mx = new(StringComparer.OrdinalIgnoreCase);

    public FakeSpfDnsResolver WithTxt(string domain, params string[] records)
    {
        _txt[domain] = records.ToList();
        return this;
    }

    public FakeSpfDnsResolver WithA(string domain, params string[] ips)
    {
        _a[domain] = ips.Select(IPAddress.Parse).ToList();
        return this;
    }

    public FakeSpfDnsResolver WithAaaa(string domain, params string[] ips)
    {
        _aaaa[domain] = ips.Select(IPAddress.Parse).ToList();
        return this;
    }

    public FakeSpfDnsResolver WithMx(string domain, params string[] exchanges)
    {
        _mx[domain] = exchanges.ToList();
        return this;
    }

    public Task<IReadOnlyList<string>> GetTxtRecordsAsync(string domain, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>(_txt.TryGetValue(domain, out var v) ? v : []);

    public Task<IReadOnlyList<IPAddress>> ResolveAAsync(string domain, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<IPAddress>>(_a.TryGetValue(domain, out var v) ? v : []);

    public Task<IReadOnlyList<IPAddress>> ResolveAaaaAsync(string domain, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<IPAddress>>(_aaaa.TryGetValue(domain, out var v) ? v : []);

    public Task<IReadOnlyList<string>> ResolveMxAsync(string domain, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>(_mx.TryGetValue(domain, out var v) ? v : []);
}
