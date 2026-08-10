namespace DmarcAnalyzer.Core.Dkim;

/// <summary>DNS abstraction used by <see cref="DkimSelectorChecker"/> so it is unit testable without real DNS.</summary>
public interface IDkimDnsResolver
{
    Task<IReadOnlyList<string>> GetSelectorTxtRecordsAsync(string selector, string domain, CancellationToken cancellationToken = default);
}
