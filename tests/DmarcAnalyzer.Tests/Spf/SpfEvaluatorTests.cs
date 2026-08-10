using System.Net;
using DmarcAnalyzer.Core.Entities;
using DmarcAnalyzer.Core.Spf;
using DmarcAnalyzer.Tests.TestDoubles;
using Xunit;

namespace DmarcAnalyzer.Tests.Spf;

public class SpfEvaluatorTests
{
    [Fact]
    public async Task Evaluate_Ip4Match_ReturnsPass()
    {
        var resolver = new FakeSpfDnsResolver().WithTxt("contoso.com", "v=spf1 ip4:203.0.113.0/24 -all");
        var result = await new SpfEvaluator(resolver).EvaluateAsync("contoso.com", IPAddress.Parse("203.0.113.10"));

        Assert.Equal(SpfResultCode.Pass, result.Result);
        Assert.Equal("ip4", result.MatchedMechanism);
    }

    [Fact]
    public async Task Evaluate_Ip4NonMatch_FallsThroughToAll()
    {
        var resolver = new FakeSpfDnsResolver().WithTxt("contoso.com", "v=spf1 ip4:203.0.113.0/24 -all");
        var result = await new SpfEvaluator(resolver).EvaluateAsync("contoso.com", IPAddress.Parse("198.51.100.5"));

        Assert.Equal(SpfResultCode.Fail, result.Result);
        Assert.Equal("all", result.MatchedMechanism);
    }

    [Theory]
    [InlineData("203.0.113.0", true)]
    [InlineData("203.0.113.255", true)]
    [InlineData("203.0.114.0", false)]
    public async Task Evaluate_Ip4CidrBoundary_IsRespected(string ip, bool expectPass)
    {
        var resolver = new FakeSpfDnsResolver().WithTxt("contoso.com", "v=spf1 ip4:203.0.113.0/24 -all");
        var result = await new SpfEvaluator(resolver).EvaluateAsync("contoso.com", IPAddress.Parse(ip));

        Assert.Equal(expectPass ? SpfResultCode.Pass : SpfResultCode.Fail, result.Result);
    }

    [Fact]
    public async Task Evaluate_Ip6CidrBoundary_IsRespected()
    {
        var resolver = new FakeSpfDnsResolver().WithTxt("contoso.com", "v=spf1 ip6:2001:db8::/32 -all");
        var evaluator = new SpfEvaluator(resolver);

        var inRange = await evaluator.EvaluateAsync("contoso.com", IPAddress.Parse("2001:db8:1234::1"));
        var outOfRange = await evaluator.EvaluateAsync("contoso.com", IPAddress.Parse("2001:db9::1"));

        Assert.Equal(SpfResultCode.Pass, inRange.Result);
        Assert.Equal(SpfResultCode.Fail, outOfRange.Result);
    }

    [Theory]
    [InlineData("v=spf1 ~all", SpfResultCode.SoftFail)]
    [InlineData("v=spf1 -all", SpfResultCode.Fail)]
    [InlineData("v=spf1 ?all", SpfResultCode.Neutral)]
    [InlineData("v=spf1 +all", SpfResultCode.Pass)]
    [InlineData("v=spf1 all", SpfResultCode.Pass)]
    public async Task Evaluate_AllQualifiers_MapToExpectedResult(string record, SpfResultCode expected)
    {
        var resolver = new FakeSpfDnsResolver().WithTxt("contoso.com", record);
        var result = await new SpfEvaluator(resolver).EvaluateAsync("contoso.com", IPAddress.Parse("198.51.100.5"));

        Assert.Equal(expected, result.Result);
    }

    [Fact]
    public async Task Evaluate_NestedIncludeTwoLevelsDeep_PropagatesPassUp()
    {
        var resolver = new FakeSpfDnsResolver()
            .WithTxt("contoso.com", "v=spf1 include:level1.example.com -all")
            .WithTxt("level1.example.com", "v=spf1 include:level2.example.com -all")
            .WithTxt("level2.example.com", "v=spf1 ip4:203.0.113.0/24 -all");

        var result = await new SpfEvaluator(resolver).EvaluateAsync("contoso.com", IPAddress.Parse("203.0.113.5"));

        Assert.Equal(SpfResultCode.Pass, result.Result);
    }

    [Fact]
    public async Task Evaluate_IncludeFail_FallsThroughToNextMechanism_NotShortCircuited()
    {
        var resolver = new FakeSpfDnsResolver()
            .WithTxt("contoso.com", "v=spf1 include:esp.example.com ip4:203.0.113.0/24 -all")
            .WithTxt("esp.example.com", "v=spf1 -all");

        var result = await new SpfEvaluator(resolver).EvaluateAsync("contoso.com", IPAddress.Parse("203.0.113.5"));

        Assert.Equal(SpfResultCode.Pass, result.Result);
        Assert.Equal("ip4", result.MatchedMechanism);
    }

    [Fact]
    public async Task Evaluate_IncludeTargetWithNoRecord_ReturnsPermError()
    {
        var resolver = new FakeSpfDnsResolver().WithTxt("contoso.com", "v=spf1 include:missing.example.com -all");

        var result = await new SpfEvaluator(resolver).EvaluateAsync("contoso.com", IPAddress.Parse("203.0.113.5"));

        Assert.Equal(SpfResultCode.PermError, result.Result);
    }

    [Fact]
    public async Task Evaluate_RedirectModifier_AppliesWhenNoMechanismMatchesAndNoAll()
    {
        var resolver = new FakeSpfDnsResolver()
            .WithTxt("contoso.com", "v=spf1 redirect=_spf.example.com")
            .WithTxt("_spf.example.com", "v=spf1 ip4:203.0.113.0/24 -all");

        var result = await new SpfEvaluator(resolver).EvaluateAsync("contoso.com", IPAddress.Parse("203.0.113.5"));

        Assert.Equal(SpfResultCode.Pass, result.Result);
    }

    [Fact]
    public async Task Evaluate_AMechanism_NoArgumentUsesCurrentDomain()
    {
        var resolver = new FakeSpfDnsResolver()
            .WithTxt("contoso.com", "v=spf1 a -all")
            .WithA("contoso.com", "203.0.113.5");

        var result = await new SpfEvaluator(resolver).EvaluateAsync("contoso.com", IPAddress.Parse("203.0.113.5"));

        Assert.Equal(SpfResultCode.Pass, result.Result);
    }

    [Fact]
    public async Task Evaluate_AMechanism_WithExplicitDomainAndCidr()
    {
        var resolver = new FakeSpfDnsResolver()
            .WithTxt("contoso.com", "v=spf1 a:mail.contoso.com/24 -all")
            .WithA("mail.contoso.com", "203.0.113.9");

        var result = await new SpfEvaluator(resolver).EvaluateAsync("contoso.com", IPAddress.Parse("203.0.113.200"));

        Assert.Equal(SpfResultCode.Pass, result.Result);
    }

    [Fact]
    public async Task Evaluate_MxMechanism_ResolvesExchangeThenItsAddress()
    {
        var resolver = new FakeSpfDnsResolver()
            .WithTxt("contoso.com", "v=spf1 mx -all")
            .WithMx("contoso.com", "mail1.contoso.com")
            .WithA("mail1.contoso.com", "203.0.113.5");

        var result = await new SpfEvaluator(resolver).EvaluateAsync("contoso.com", IPAddress.Parse("203.0.113.5"));

        Assert.Equal(SpfResultCode.Pass, result.Result);
    }

    [Fact]
    public async Task Evaluate_ExistsMechanism_MatchesWhenTargetResolves()
    {
        var resolver = new FakeSpfDnsResolver()
            .WithTxt("contoso.com", "v=spf1 exists:sentinel.contoso.com -all")
            .WithA("sentinel.contoso.com", "127.0.0.1");

        var result = await new SpfEvaluator(resolver).EvaluateAsync("contoso.com", IPAddress.Parse("9.9.9.9"));

        Assert.Equal(SpfResultCode.Pass, result.Result);
    }

    [Fact]
    public async Task Evaluate_ExceedingTenLookups_ReturnsPermError()
    {
        var resolver = new FakeSpfDnsResolver();
        resolver.WithTxt("contoso.com", "v=spf1 include:l1.example.com -all");
        for (var i = 1; i <= 11; i++)
        {
            resolver.WithTxt($"l{i}.example.com", $"v=spf1 include:l{i + 1}.example.com -all");
        }
        resolver.WithTxt("l12.example.com", "v=spf1 -all");

        var result = await new SpfEvaluator(resolver).EvaluateAsync("contoso.com", IPAddress.Parse("203.0.113.5"));

        Assert.Equal(SpfResultCode.PermError, result.Result);
    }

    [Fact]
    public async Task Evaluate_MultipleSpfRecords_ReturnsPermError()
    {
        var resolver = new FakeSpfDnsResolver().WithTxt("contoso.com", "v=spf1 -all", "v=spf1 ~all");

        var result = await new SpfEvaluator(resolver).EvaluateAsync("contoso.com", IPAddress.Parse("203.0.113.5"));

        Assert.Equal(SpfResultCode.PermError, result.Result);
    }

    [Fact]
    public async Task Evaluate_NoSpfRecord_ReturnsNone()
    {
        var resolver = new FakeSpfDnsResolver();

        var result = await new SpfEvaluator(resolver).EvaluateAsync("contoso.com", IPAddress.Parse("203.0.113.5"));

        Assert.Equal(SpfResultCode.None, result.Result);
    }

    [Fact]
    public async Task Evaluate_UnknownMechanism_ReturnsPermError()
    {
        var resolver = new FakeSpfDnsResolver().WithTxt("contoso.com", "v=spf1 bogus:thing -all");

        var result = await new SpfEvaluator(resolver).EvaluateAsync("contoso.com", IPAddress.Parse("203.0.113.5"));

        Assert.Equal(SpfResultCode.PermError, result.Result);
    }

    [Fact]
    public async Task Evaluate_UnknownModifier_IsIgnoredPerRfc7208Extensibility()
    {
        var resolver = new FakeSpfDnsResolver().WithTxt("contoso.com", "v=spf1 ip4:203.0.113.0/24 unknown-mod=somevalue -all");

        var result = await new SpfEvaluator(resolver).EvaluateAsync("contoso.com", IPAddress.Parse("203.0.113.5"));

        Assert.Equal(SpfResultCode.Pass, result.Result);
    }

    [Fact]
    public async Task Evaluate_MechanismKeywordsAreCaseInsensitive()
    {
        var resolver = new FakeSpfDnsResolver().WithTxt("contoso.com", "v=spf1 IP4:203.0.113.0/24 -ALL");

        var result = await new SpfEvaluator(resolver).EvaluateAsync("contoso.com", IPAddress.Parse("203.0.113.5"));

        Assert.Equal(SpfResultCode.Pass, result.Result);
    }
}
