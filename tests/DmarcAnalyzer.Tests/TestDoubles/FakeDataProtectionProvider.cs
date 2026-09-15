using Microsoft.AspNetCore.DataProtection;

namespace DmarcAnalyzer.Tests.TestDoubles;

/// <summary>Reversible byte-reversal stand-in for the real Data Protection stack — enough to
/// exercise a protect/unprotect round trip without pulling in the full crypto implementation.</summary>
public class FakeDataProtectionProvider : IDataProtectionProvider
{
    public IDataProtector CreateProtector(string purpose) => new FakeDataProtector();
}

public class FakeDataProtector : IDataProtector
{
    public IDataProtector CreateProtector(string purpose) => this;

    public byte[] Protect(byte[] plaintext) => plaintext.Reverse().ToArray();

    public byte[] Unprotect(byte[] protectedData) => protectedData.Reverse().ToArray();
}
