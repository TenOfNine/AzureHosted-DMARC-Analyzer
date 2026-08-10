using System.Net;

namespace DmarcAnalyzer.Core.Spf;

/// <summary>
/// DNS abstraction used by <see cref="SpfEvaluator"/> so the evaluator has zero direct network
/// dependency and can be unit tested with an in-memory fake.
/// </summary>
public interface ISpfDnsResolver
{
    Task<IReadOnlyList<string>> GetTxtRecordsAsync(string domain, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IPAddress>> ResolveAAsync(string domain, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IPAddress>> ResolveAaaaAsync(string domain, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ResolveMxAsync(string domain, CancellationToken cancellationToken = default);
}
