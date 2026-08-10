using DmarcAnalyzer.Core.Entities;

namespace DmarcAnalyzer.Core.Spf;

public sealed record SpfEvaluationOutcome(SpfResultCode Result, string? MatchedMechanism, int LookupCount, string? RecordText);
