namespace DmarcAnalyzer.Core.Entities;

/// <summary>One auth_results/dkim entry as reported by the receiving mail server.</summary>
public class DkimAuthResult
{
    public Guid Id { get; set; }

    public Guid DmarcRecordId { get; set; }
    public DmarcRecord DmarcRecord { get; set; } = null!;

    public string Domain { get; set; } = string.Empty;
    public string? Selector { get; set; }
    public DkimResultCode Result { get; set; }
    public string? HumanResult { get; set; }

    /// <summary>DNS liveness check of this selector, performed independently of the reported result.</summary>
    public DkimSelectorCheck? SelectorCheck { get; set; }
}
