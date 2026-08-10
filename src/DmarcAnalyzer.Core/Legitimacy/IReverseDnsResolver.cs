using System.Net;

namespace DmarcAnalyzer.Core.Legitimacy;

/// <summary>DNS abstraction for the reverse (PTR) lookup used in sender-legitimacy scoring — kept separate from ISpfDnsResolver/IDkimDnsResolver since it serves a different, unrelated check.</summary>
public interface IReverseDnsResolver
{
    /// <summary>Returns the PTR hostname for the given IP, or null if none exists.</summary>
    Task<string?> GetPtrHostnameAsync(IPAddress ip, CancellationToken cancellationToken = default);
}
