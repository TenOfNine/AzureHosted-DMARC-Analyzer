using System.Net;
using System.Net.Sockets;
using DmarcAnalyzer.Core.Entities;

namespace DmarcAnalyzer.Core.Spf;

/// <summary>
/// Recursive RFC 7208 SPF evaluator. Macro expansion (RFC 7208 §7) is intentionally not implemented —
/// "exists"/"include"/"redirect" arguments are treated as literal domains, which covers the overwhelming
/// majority of real-world SPF records and keeps this auditable; a record relying on macros will fall
/// through as a mechanism mismatch rather than mis-evaluating silently.
/// </summary>
public class SpfEvaluator(ISpfDnsResolver dnsResolver)
{
    private const int MaxLookups = 10;

    public async Task<SpfEvaluationOutcome> EvaluateAsync(string domain, IPAddress checkedIp, CancellationToken cancellationToken = default)
    {
        var lookupCount = new LookupCounter();
        var (result, matchedMechanism, recordText) = await EvaluateDomainAsync(domain, checkedIp, lookupCount, cancellationToken);
        return new SpfEvaluationOutcome(result, matchedMechanism, lookupCount.Count, recordText);
    }

    private async Task<(SpfResultCode Result, string? MatchedMechanism, string? RecordText)> EvaluateDomainAsync(
        string domain, IPAddress checkedIp, LookupCounter lookupCount, CancellationToken cancellationToken)
    {
        var txtRecords = await dnsResolver.GetTxtRecordsAsync(domain, cancellationToken);
        var spfRecords = txtRecords.Where(t => t.StartsWith("v=spf1", StringComparison.OrdinalIgnoreCase)).ToList();

        if (spfRecords.Count == 0)
        {
            return (SpfResultCode.None, null, null);
        }

        if (spfRecords.Count > 1)
        {
            return (SpfResultCode.PermError, null, string.Join(" | ", spfRecords));
        }

        var recordText = spfRecords[0];
        var terms = recordText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1);

        string? redirectDomain = null;

        foreach (var term in terms)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (TryParseModifier(term, out var modifierName, out var modifierValue))
            {
                if (modifierName.Equals("redirect", StringComparison.OrdinalIgnoreCase))
                {
                    redirectDomain = modifierValue;
                }
                // "exp" and any other modifier are ignored per RFC 7208 §6 (extensibility).
                continue;
            }

            var (qualifier, mechanism, value) = ParseMechanism(term);
            var mechanismLower = mechanism.ToLowerInvariant();

            if (mechanismLower == "all")
            {
                return (QualifierToResult(qualifier), "all", recordText);
            }

            if (RequiresLookup(mechanismLower) && !lookupCount.TryIncrement(MaxLookups))
            {
                return (SpfResultCode.PermError, null, recordText);
            }

            bool matched;
            switch (mechanismLower)
            {
                case "ip4":
                case "ip6":
                    matched = MatchesIpMechanism(value, mechanismLower, checkedIp);
                    break;

                case "a":
                    matched = await MatchesAAsync(value, domain, checkedIp, cancellationToken);
                    break;

                case "mx":
                    matched = await MatchesMxAsync(value, domain, checkedIp, cancellationToken);
                    break;

                case "exists":
                    matched = await MatchesExistsAsync(value, domain, cancellationToken);
                    break;

                case "include":
                    {
                        if (string.IsNullOrEmpty(value))
                        {
                            return (SpfResultCode.PermError, null, recordText);
                        }

                        var (includeResult, _, _) = await EvaluateDomainAsync(value, checkedIp, lookupCount, cancellationToken);
                        switch (includeResult)
                        {
                            case SpfResultCode.Pass:
                                matched = true;
                                break;
                            case SpfResultCode.None or SpfResultCode.PermError:
                                // Per RFC 7208 §5.2: an "include" target with no record, or one that itself
                                // PermErrors, is a PermError for the outer evaluation.
                                return (SpfResultCode.PermError, null, recordText);
                            case SpfResultCode.TempError:
                                return (SpfResultCode.TempError, null, recordText);
                            default:
                                // Fail/SoftFail/Neutral from the included domain: falls through, not a match.
                                matched = false;
                                break;
                        }

                        break;
                    }

                case "ptr":
                    // Deprecated by RFC 7208 §5.5 and intentionally unsupported.
                    return (SpfResultCode.PermError, null, recordText);

                default:
                    // Syntactically-unrecognized mechanism.
                    return (SpfResultCode.PermError, null, recordText);
            }

            if (matched)
            {
                return (QualifierToResult(qualifier), mechanismLower, recordText);
            }
        }

        if (redirectDomain is not null)
        {
            if (!lookupCount.TryIncrement(MaxLookups))
            {
                return (SpfResultCode.PermError, null, recordText);
            }

            var (redirectResult, redirectMechanism, _) = await EvaluateDomainAsync(redirectDomain, checkedIp, lookupCount, cancellationToken);
            // An empty/missing record at the redirect target is a PermError, per RFC 7208 §6.1.
            var finalResult = redirectResult == SpfResultCode.None ? SpfResultCode.PermError : redirectResult;
            return (finalResult, redirectMechanism, recordText);
        }

        // No mechanism matched, no "all", no "redirect": implicit result, equivalent to a trailing "?all".
        return (SpfResultCode.Neutral, null, recordText);
    }

    private static bool MatchesIpMechanism(string? value, string mechanismName, IPAddress checkedIp)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var isIp6 = mechanismName == "ip6";
        var slashIndex = value.IndexOf('/');
        var addressText = slashIndex >= 0 ? value[..slashIndex] : value;
        var prefixText = slashIndex >= 0 ? value[(slashIndex + 1)..] : (isIp6 ? "128" : "32");

        if (!IPAddress.TryParse(addressText, out var networkAddress) || !int.TryParse(prefixText, out var prefixLength))
        {
            return false;
        }

        return IsInCidr(networkAddress, checkedIp, prefixLength);
    }

    private async Task<bool> MatchesAAsync(string? value, string currentDomain, IPAddress checkedIp, CancellationToken cancellationToken)
    {
        var (domain, cidr4, cidr6) = SplitDomainCidr(value, currentDomain);

        if (checkedIp.AddressFamily == AddressFamily.InterNetwork)
        {
            var addresses = await dnsResolver.ResolveAAsync(domain, cancellationToken);
            return addresses.Any(a => IsInCidr(a, checkedIp, cidr4 ?? 32));
        }

        var addresses6 = await dnsResolver.ResolveAaaaAsync(domain, cancellationToken);
        return addresses6.Any(a => IsInCidr(a, checkedIp, cidr6 ?? 128));
    }

    private async Task<bool> MatchesMxAsync(string? value, string currentDomain, IPAddress checkedIp, CancellationToken cancellationToken)
    {
        var (domain, cidr4, cidr6) = SplitDomainCidr(value, currentDomain);
        var exchanges = await dnsResolver.ResolveMxAsync(domain, cancellationToken);

        foreach (var exchange in exchanges)
        {
            if (checkedIp.AddressFamily == AddressFamily.InterNetwork)
            {
                var addresses = await dnsResolver.ResolveAAsync(exchange, cancellationToken);
                if (addresses.Any(a => IsInCidr(a, checkedIp, cidr4 ?? 32)))
                {
                    return true;
                }
            }
            else
            {
                var addresses6 = await dnsResolver.ResolveAaaaAsync(exchange, cancellationToken);
                if (addresses6.Any(a => IsInCidr(a, checkedIp, cidr6 ?? 128)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private async Task<bool> MatchesExistsAsync(string? value, string currentDomain, CancellationToken cancellationToken)
    {
        var domain = string.IsNullOrEmpty(value) ? currentDomain : value;
        var addresses = await dnsResolver.ResolveAAsync(domain, cancellationToken);
        return addresses.Count > 0;
    }

    /// <summary>
    /// Whether <paramref name="checkedIp"/> falls within the /<paramref name="prefixLength"/> network
    /// derived from <paramref name="candidate"/>. Implemented as a manual bitwise prefix comparison
    /// rather than <see cref="IPNetwork"/>, because <c>candidate</c> here is typically a single resolved
    /// A/AAAA host address (not a pre-masked network base address), and IPNetwork's constructor throws
    /// if the base address has non-zero bits past the prefix.
    /// </summary>
    private static bool IsInCidr(IPAddress candidate, IPAddress checkedIp, int prefixLength)
    {
        if (candidate.AddressFamily != checkedIp.AddressFamily)
        {
            return false;
        }

        var candidateBytes = candidate.GetAddressBytes();
        var checkedBytes = checkedIp.GetAddressBytes();
        var bits = Math.Clamp(prefixLength, 0, candidateBytes.Length * 8);

        var fullBytes = bits / 8;
        for (var i = 0; i < fullBytes; i++)
        {
            if (candidateBytes[i] != checkedBytes[i])
            {
                return false;
            }
        }

        var remainingBits = bits % 8;
        if (remainingBits == 0)
        {
            return true;
        }

        var mask = (byte)(0xFF << (8 - remainingBits));
        return (candidateBytes[fullBytes] & mask) == (checkedBytes[fullBytes] & mask);
    }

    private static (string Domain, int? Cidr4, int? Cidr6) SplitDomainCidr(string? value, string currentDomain)
    {
        if (string.IsNullOrEmpty(value))
        {
            return (currentDomain, null, null);
        }

        var dualSlashIndex = value.IndexOf("//", StringComparison.Ordinal);
        var ip4Part = dualSlashIndex >= 0 ? value[..dualSlashIndex] : value;
        var ip6Part = dualSlashIndex >= 0 ? value[(dualSlashIndex + 2)..] : null;

        string domainPart;
        int? cidr4 = null;
        var slashIndex = ip4Part.IndexOf('/');
        if (slashIndex >= 0)
        {
            domainPart = ip4Part[..slashIndex];
            if (int.TryParse(ip4Part[(slashIndex + 1)..], out var c4))
            {
                cidr4 = c4;
            }
        }
        else
        {
            domainPart = ip4Part;
        }

        int? cidr6 = null;
        if (ip6Part is not null && int.TryParse(ip6Part, out var c6))
        {
            cidr6 = c6;
        }

        if (string.IsNullOrEmpty(domainPart))
        {
            domainPart = currentDomain;
        }

        return (domainPart, cidr4, cidr6);
    }

    private static bool RequiresLookup(string mechanismLower) =>
        mechanismLower is "a" or "mx" or "include" or "exists" or "ptr";

    private static SpfResultCode QualifierToResult(char qualifier) => qualifier switch
    {
        '+' => SpfResultCode.Pass,
        '-' => SpfResultCode.Fail,
        '~' => SpfResultCode.SoftFail,
        '?' => SpfResultCode.Neutral,
        _ => SpfResultCode.Neutral
    };

    private static (char Qualifier, string Name, string? Value) ParseMechanism(string term)
    {
        var qualifier = '+';
        var rest = term;
        if (rest.Length > 0 && "+-~?".IndexOf(rest[0]) >= 0)
        {
            qualifier = rest[0];
            rest = rest[1..];
        }

        var colonIndex = rest.IndexOf(':');
        var slashIndex = rest.IndexOf('/');

        if (colonIndex >= 0 && (slashIndex < 0 || colonIndex < slashIndex))
        {
            return (qualifier, rest[..colonIndex], rest[(colonIndex + 1)..]);
        }

        if (slashIndex >= 0)
        {
            return (qualifier, rest[..slashIndex], rest[slashIndex..]);
        }

        return (qualifier, rest, null);
    }

    private static bool TryParseModifier(string term, out string name, out string value)
    {
        var eqIndex = term.IndexOf('=');
        if (eqIndex > 0)
        {
            name = term[..eqIndex];
            value = term[(eqIndex + 1)..];
            return true;
        }

        name = string.Empty;
        value = string.Empty;
        return false;
    }

    private sealed class LookupCounter
    {
        public int Count { get; private set; }

        public bool TryIncrement(int max)
        {
            if (Count >= max)
            {
                return false;
            }

            Count++;
            return true;
        }
    }
}
