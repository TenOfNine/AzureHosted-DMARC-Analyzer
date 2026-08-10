using DmarcAnalyzer.Core.Dkim;
using DmarcAnalyzer.Core.Entities;
using DmarcAnalyzer.Tests.TestDoubles;
using Xunit;

namespace DmarcAnalyzer.Tests.Dkim;

public class DkimSelectorCheckerTests
{
    [Fact]
    public async Task Check_ValidKey_ReturnsValid()
    {
        var validBase64Key = Convert.ToBase64String(new byte[32]);
        var resolver = new FakeDkimDnsResolver().WithSelector("selector1", "contoso.com", $"v=DKIM1; k=rsa; p={validBase64Key}");

        var result = await new DkimSelectorChecker(resolver).CheckAsync("contoso.com", "selector1");

        Assert.Equal(DkimSelectorStatus.Valid, result.Status);
    }

    [Fact]
    public async Task Check_EmptyPTag_ReturnsRevoked()
    {
        var resolver = new FakeDkimDnsResolver().WithSelector("selector1", "contoso.com", "v=DKIM1; k=rsa; p=");

        var result = await new DkimSelectorChecker(resolver).CheckAsync("contoso.com", "selector1");

        Assert.Equal(DkimSelectorStatus.Revoked, result.Status);
    }

    [Fact]
    public async Task Check_NoTxtRecord_ReturnsMissing()
    {
        var resolver = new FakeDkimDnsResolver();

        var result = await new DkimSelectorChecker(resolver).CheckAsync("contoso.com", "selector1");

        Assert.Equal(DkimSelectorStatus.Missing, result.Status);
    }

    [Fact]
    public async Task Check_RecordWithoutPTag_ReturnsParseError()
    {
        var resolver = new FakeDkimDnsResolver().WithSelector("selector1", "contoso.com", "v=DKIM1; k=rsa");

        var result = await new DkimSelectorChecker(resolver).CheckAsync("contoso.com", "selector1");

        Assert.Equal(DkimSelectorStatus.ParseError, result.Status);
    }

    [Fact]
    public async Task Check_GarbageBase64_ReturnsParseError()
    {
        var resolver = new FakeDkimDnsResolver().WithSelector("selector1", "contoso.com", "v=DKIM1; k=rsa; p=not-valid-base64!!");

        var result = await new DkimSelectorChecker(resolver).CheckAsync("contoso.com", "selector1");

        Assert.Equal(DkimSelectorStatus.ParseError, result.Status);
    }
}
