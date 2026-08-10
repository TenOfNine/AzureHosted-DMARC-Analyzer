using DmarcAnalyzer.Core.Entities;

namespace DmarcAnalyzer.Core.Dkim;

public sealed record DkimSelectorCheckResult(DkimSelectorStatus Status, string? RawTxtRecord);
