namespace DmarcAnalyzer.Core.Entities;

/// <summary>One auth_results/spf entry as reported by the receiving mail server.</summary>
public class SpfAuthResult
{
    public Guid Id { get; set; }

    public Guid DmarcRecordId { get; set; }
    public DmarcRecord DmarcRecord { get; set; } = null!;

    public string Domain { get; set; } = string.Empty;
    public SpfScope Scope { get; set; } = SpfScope.MfFrom;
    public SpfResultCode Result { get; set; }
}
