namespace DmarcAnalyzer.Core.Abstractions;

/// <summary>
/// Reports whether this deployment has completed its first-run setup (Graph connection + at least one
/// domain + at least one mailbox + retention configured). Gates the rest of the UI until true, which is
/// what makes the same codebase redeployable per customer without code changes.
/// </summary>
public interface ISetupStateService
{
    Task<bool> IsSetupCompleteAsync(CancellationToken cancellationToken = default);
}
