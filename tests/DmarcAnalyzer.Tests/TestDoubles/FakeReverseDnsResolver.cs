using System.Net;
using DmarcAnalyzer.Core.Legitimacy;

namespace DmarcAnalyzer.Tests.TestDoubles;

public class FakeReverseDnsResolver : IReverseDnsResolver
{
    private readonly Dictionary<string, string> _ptrRecords = new();

    public FakeReverseDnsResolver WithPtr(string ip, string hostname)
    {
        _ptrRecords[ip] = hostname;
        return this;
    }

    public Task<string?> GetPtrHostnameAsync(IPAddress ip, CancellationToken cancellationToken = default) =>
        Task.FromResult(_ptrRecords.TryGetValue(ip.ToString(), out var hostname) ? hostname : null);
}
