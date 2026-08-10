using DmarcAnalyzer.Core.Entities;

namespace DmarcAnalyzer.Core.Dkim;

/// <summary>
/// DNS-liveness check of a DKIM selector's public key record ("{selector}._domainkey.{domain}"),
/// independent of whatever result a historical DMARC report claimed for it.
/// </summary>
public class DkimSelectorChecker(IDkimDnsResolver dnsResolver)
{
    public async Task<DkimSelectorCheckResult> CheckAsync(string domain, string selector, CancellationToken cancellationToken = default)
    {
        var txtRecords = await dnsResolver.GetSelectorTxtRecordsAsync(selector, domain, cancellationToken);
        if (txtRecords.Count == 0)
        {
            return new DkimSelectorCheckResult(DkimSelectorStatus.Missing, null);
        }

        var keyRecord = txtRecords.FirstOrDefault(r => r.Contains("p=", StringComparison.Ordinal)) ?? txtRecords[0];
        var tags = ParseTags(keyRecord);

        if (!tags.TryGetValue("p", out var publicKey))
        {
            return new DkimSelectorCheckResult(DkimSelectorStatus.ParseError, keyRecord);
        }

        // An empty p= tag is an explicitly revoked key per RFC 6376 §3.6.1.
        if (string.IsNullOrEmpty(publicKey))
        {
            return new DkimSelectorCheckResult(DkimSelectorStatus.Revoked, keyRecord);
        }

        if (!IsValidBase64(publicKey))
        {
            return new DkimSelectorCheckResult(DkimSelectorStatus.ParseError, keyRecord);
        }

        return new DkimSelectorCheckResult(DkimSelectorStatus.Valid, keyRecord);
    }

    private static Dictionary<string, string> ParseTags(string record)
    {
        var tags = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in record.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = part.Trim();
            var eqIndex = trimmed.IndexOf('=');
            if (eqIndex <= 0)
            {
                continue;
            }

            var key = trimmed[..eqIndex].Trim();
            var value = trimmed[(eqIndex + 1)..].Trim();
            tags[key] = value;
        }

        return tags;
    }

    private static bool IsValidBase64(string value)
    {
        // Some DNS providers reinsert whitespace when concatenating long, multi-string TXT records.
        var cleaned = value.Replace(" ", string.Empty).Replace("\t", string.Empty).Replace("\n", string.Empty);
        if (cleaned.Length == 0 || cleaned.Length % 4 != 0)
        {
            return false;
        }

        Span<byte> buffer = new byte[cleaned.Length / 4 * 3];
        return Convert.TryFromBase64String(cleaned, buffer, out _);
    }
}
