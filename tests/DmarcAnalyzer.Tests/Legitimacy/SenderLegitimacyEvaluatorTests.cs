using DmarcAnalyzer.Core.Entities;
using DmarcAnalyzer.Core.Legitimacy;
using Xunit;

namespace DmarcAnalyzer.Tests.Legitimacy;

public class SenderLegitimacyEvaluatorTests
{
    [Fact]
    public void Evaluate_OverrideMatch_IsAlwaysVerified_EvenWithNoOtherPositiveSignal()
    {
        var signals = new SenderLegitimacySignals(
            AlignedPassRatio: 0,
            CurrentSpfResult: SpfResultCode.Fail,
            CurrentDkimStale: true,
            HasReverseDns: false,
            ForwardConfirmed: false,
            IsOverrideMatch: true);

        Assert.Equal(SenderLegitimacyLevel.Verified, SenderLegitimacyEvaluator.Evaluate(signals));
    }

    [Fact]
    public void Evaluate_HighAlignedPassRatio_CurrentSpfPass_NotStale_IsVerified()
    {
        var signals = new SenderLegitimacySignals(
            AlignedPassRatio: 0.99,
            CurrentSpfResult: SpfResultCode.Pass,
            CurrentDkimStale: false,
            HasReverseDns: true,
            ForwardConfirmed: true,
            IsOverrideMatch: false);

        Assert.Equal(SenderLegitimacyLevel.Verified, SenderLegitimacyEvaluator.Evaluate(signals));
    }

    [Fact]
    public void Evaluate_ExactlyAtVerifiedThreshold_IsVerified()
    {
        var signals = new SenderLegitimacySignals(
            AlignedPassRatio: SenderLegitimacyEvaluator.VerifiedAlignedPassRatioThreshold,
            CurrentSpfResult: SpfResultCode.Pass,
            CurrentDkimStale: false,
            HasReverseDns: true,
            ForwardConfirmed: false,
            IsOverrideMatch: false);

        Assert.Equal(SenderLegitimacyLevel.Verified, SenderLegitimacyEvaluator.Evaluate(signals));
    }

    [Fact]
    public void Evaluate_HighHistoricalPassRatio_ButLiveSpfNowFails_DropsToLikelyLegitimate()
    {
        // This is the "drift" case: a domain that used to authorize this sender, and mostly did in
        // its report history, but whose SPF record no longer covers it today.
        var signals = new SenderLegitimacySignals(
            AlignedPassRatio: 0.99,
            CurrentSpfResult: SpfResultCode.Fail,
            CurrentDkimStale: false,
            HasReverseDns: true,
            ForwardConfirmed: false,
            IsOverrideMatch: false);

        Assert.Equal(SenderLegitimacyLevel.LikelyLegitimate, SenderLegitimacyEvaluator.Evaluate(signals));
    }

    [Fact]
    public void Evaluate_HighPassRatio_CurrentSpfPass_ButDkimStale_DropsToLikelyLegitimate()
    {
        var signals = new SenderLegitimacySignals(
            AlignedPassRatio: 0.99,
            CurrentSpfResult: SpfResultCode.Pass,
            CurrentDkimStale: true,
            HasReverseDns: true,
            ForwardConfirmed: false,
            IsOverrideMatch: false);

        Assert.Equal(SenderLegitimacyLevel.LikelyLegitimate, SenderLegitimacyEvaluator.Evaluate(signals));
    }

    [Fact]
    public void Evaluate_ModerateAlignedPassRatio_IsLikelyLegitimate()
    {
        var signals = new SenderLegitimacySignals(
            AlignedPassRatio: 0.6,
            CurrentSpfResult: SpfResultCode.Fail,
            CurrentDkimStale: false,
            HasReverseDns: true,
            ForwardConfirmed: false,
            IsOverrideMatch: false);

        Assert.Equal(SenderLegitimacyLevel.LikelyLegitimate, SenderLegitimacyEvaluator.Evaluate(signals));
    }

    [Fact]
    public void Evaluate_ZeroHistoricalVolume_ButCurrentSpfPasses_IsLikelyLegitimate()
    {
        // A brand-new sender with no report history yet, but which the live SPF record already covers.
        var signals = new SenderLegitimacySignals(
            AlignedPassRatio: 0,
            CurrentSpfResult: SpfResultCode.Pass,
            CurrentDkimStale: false,
            HasReverseDns: false,
            ForwardConfirmed: false,
            IsOverrideMatch: false);

        Assert.Equal(SenderLegitimacyLevel.LikelyLegitimate, SenderLegitimacyEvaluator.Evaluate(signals));
    }

    [Fact]
    public void Evaluate_ForwardConfirmedReverseDnsAlone_IsLikelyLegitimate()
    {
        var signals = new SenderLegitimacySignals(
            AlignedPassRatio: 0,
            CurrentSpfResult: SpfResultCode.Fail,
            CurrentDkimStale: false,
            HasReverseDns: true,
            ForwardConfirmed: true,
            IsOverrideMatch: false);

        Assert.Equal(SenderLegitimacyLevel.LikelyLegitimate, SenderLegitimacyEvaluator.Evaluate(signals));
    }

    [Fact]
    public void Evaluate_NoPositiveSignalAndNoReverseDns_IsSuspicious()
    {
        var signals = new SenderLegitimacySignals(
            AlignedPassRatio: 0,
            CurrentSpfResult: SpfResultCode.Fail,
            CurrentDkimStale: false,
            HasReverseDns: false,
            ForwardConfirmed: false,
            IsOverrideMatch: false);

        Assert.Equal(SenderLegitimacyLevel.Suspicious, SenderLegitimacyEvaluator.Evaluate(signals));
    }

    [Fact]
    public void Evaluate_NoPositiveSignal_ButHasReverseDns_IsUnverifiedNotSuspicious()
    {
        var signals = new SenderLegitimacySignals(
            AlignedPassRatio: 0.1,
            CurrentSpfResult: SpfResultCode.Fail,
            CurrentDkimStale: false,
            HasReverseDns: true,
            ForwardConfirmed: false,
            IsOverrideMatch: false);

        Assert.Equal(SenderLegitimacyLevel.Unverified, SenderLegitimacyEvaluator.Evaluate(signals));
    }

    [Theory]
    [InlineData(SpfResultCode.None)]
    [InlineData(SpfResultCode.Neutral)]
    [InlineData(SpfResultCode.SoftFail)]
    [InlineData(SpfResultCode.TempError)]
    [InlineData(SpfResultCode.PermError)]
    public void Evaluate_AnyNonPassCurrentSpfResult_DoesNotAloneReachVerified(SpfResultCode result)
    {
        var signals = new SenderLegitimacySignals(
            AlignedPassRatio: 0.99,
            CurrentSpfResult: result,
            CurrentDkimStale: false,
            HasReverseDns: true,
            ForwardConfirmed: false,
            IsOverrideMatch: false);

        Assert.NotEqual(SenderLegitimacyLevel.Verified, SenderLegitimacyEvaluator.Evaluate(signals));
    }
}
