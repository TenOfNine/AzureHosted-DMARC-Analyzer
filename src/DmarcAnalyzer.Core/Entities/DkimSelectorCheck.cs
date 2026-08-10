namespace DmarcAnalyzer.Core.Entities;

/// <summary>
/// DNS-based liveness check of "{selector}._domainkey.{domain}", performed independently of the
/// report's own DKIM verdict, to catch selectors that have since been rotated or removed.
/// </summary>
public class DkimSelectorCheck
{
    public Guid Id { get; set; }

    public Guid DkimAuthResultId { get; set; }
    public DkimAuthResult DkimAuthResult { get; set; } = null!;

    public DateTime CheckedUtc { get; set; } = DateTime.UtcNow;
    public DkimSelectorStatus Status { get; set; }
    public string? RawTxtRecord { get; set; }

    /// <summary>True when the historical report says Pass but the selector no longer resolves/is revoked today.</summary>
    public bool StaleFlag { get; set; }
}
