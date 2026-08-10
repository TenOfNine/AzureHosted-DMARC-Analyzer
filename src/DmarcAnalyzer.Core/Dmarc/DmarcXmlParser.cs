using System.Xml;
using System.Xml.Linq;

namespace DmarcAnalyzer.Core.Dmarc;

/// <summary>
/// Parses an RFC 7489 aggregate report ("feedback" root element) XML stream. Tolerant of the schema's
/// optional fields (extra_contact_info, comment, sp, pct, fo, dkim selector/human_result); throws a
/// typed <see cref="DmarcParseException"/> — never lets a malformed report crash the ingestion loop —
/// on malformed XML or a missing required field.
/// </summary>
public static class DmarcXmlParser
{
    public static ParsedDmarcReport Parse(Stream xmlStream)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Load(xmlStream, LoadOptions.None);
        }
        catch (Exception ex) when (ex is XmlException or InvalidOperationException)
        {
            throw new DmarcParseException("The attachment is not well-formed XML.", ex);
        }

        var feedback = doc.Root;
        if (feedback is null || feedback.Name.LocalName != "feedback")
        {
            throw new DmarcParseException("Root element is not <feedback> — this is not a DMARC aggregate report.");
        }

        var metadataEl = feedback.Element("report_metadata")
            ?? throw new DmarcParseException("Missing required <report_metadata> element.");
        var policyEl = feedback.Element("policy_published")
            ?? throw new DmarcParseException("Missing required <policy_published> element.");

        var metadata = ParseMetadata(metadataEl);
        var policy = ParsePolicy(policyEl);
        var records = feedback.Elements("record").Select(ParseRecord).ToList();

        return new ParsedDmarcReport(metadata, policy, records);
    }

    private static ParsedReportMetadata ParseMetadata(XElement el)
    {
        var reportId = RequireElement(el, "report_id");
        var orgName = RequireElement(el, "org_name");
        var email = RequireElement(el, "email");
        var extraContactInfo = el.Element("extra_contact_info")?.Value;

        var dateRangeEl = el.Element("date_range")
            ?? throw new DmarcParseException("Missing required <date_range> element in report_metadata.");
        var begin = RequireUnixSeconds(dateRangeEl, "begin");
        var end = RequireUnixSeconds(dateRangeEl, "end");

        return new ParsedReportMetadata(orgName, email, extraContactInfo, reportId, begin, end);
    }

    private static ParsedPolicyPublished ParsePolicy(XElement el)
    {
        var domain = RequireElement(el, "domain");
        var adkim = el.Element("adkim")?.Value ?? "r";
        var aspf = el.Element("aspf")?.Value ?? "r";
        var p = RequireElement(el, "p");
        var sp = el.Element("sp")?.Value;
        var pctText = el.Element("pct")?.Value;
        var pct = int.TryParse(pctText, out var pctValue) ? pctValue : 100;
        var fo = el.Element("fo")?.Value;

        return new ParsedPolicyPublished(domain, adkim, aspf, p, sp, pct, fo);
    }

    private static ParsedDmarcRecord ParseRecord(XElement recordEl)
    {
        var rowEl = recordEl.Element("row")
            ?? throw new DmarcParseException("Record missing required <row> element.");
        var sourceIp = RequireElement(rowEl, "source_ip");
        var countText = RequireElement(rowEl, "count");
        if (!int.TryParse(countText, out var count))
        {
            throw new DmarcParseException($"Record has non-numeric <count>: '{countText}'.");
        }

        var policyEvaluatedEl = rowEl.Element("policy_evaluated")
            ?? throw new DmarcParseException("Record row missing required <policy_evaluated> element.");
        var disposition = RequireElement(policyEvaluatedEl, "disposition");
        var pDkim = RequireElement(policyEvaluatedEl, "dkim");
        var pSpf = RequireElement(policyEvaluatedEl, "spf");
        var reasons = policyEvaluatedEl.Elements("reason")
            .Select(r => r.Element("type")?.Value ?? "other")
            .ToList();

        var identifiersEl = recordEl.Element("identifiers");
        var headerFrom = identifiersEl?.Element("header_from")?.Value
            ?? throw new DmarcParseException("Record missing required <identifiers><header_from>.");
        var envelopeFrom = identifiersEl.Element("envelope_from")?.Value;
        var envelopeTo = identifiersEl.Element("envelope_to")?.Value;

        var authResultsEl = recordEl.Element("auth_results")
            ?? throw new DmarcParseException("Record missing required <auth_results> element.");

        var dkimResults = authResultsEl.Elements("dkim").Select(ParseDkim).ToList();
        var spfResults = authResultsEl.Elements("spf").Select(ParseSpf).ToList();

        return new ParsedDmarcRecord(
            sourceIp, count, disposition, pDkim, pSpf,
            reasons.Count > 0 ? reasons : null,
            headerFrom, envelopeFrom, envelopeTo,
            dkimResults, spfResults);
    }

    private static ParsedDkimAuthResult ParseDkim(XElement el)
    {
        var domain = RequireElement(el, "domain");
        var selector = el.Element("selector")?.Value;
        var result = RequireElement(el, "result");
        var humanResult = el.Element("human_result")?.Value;
        return new ParsedDkimAuthResult(domain, selector, result, humanResult);
    }

    private static ParsedSpfAuthResult ParseSpf(XElement el)
    {
        var domain = RequireElement(el, "domain");
        var scope = el.Element("scope")?.Value;
        var result = RequireElement(el, "result");
        return new ParsedSpfAuthResult(domain, scope, result);
    }

    private static string RequireElement(XElement parent, string name)
    {
        var value = parent.Element(name)?.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DmarcParseException($"Missing required <{name}> element under <{parent.Name.LocalName}>.");
        }

        return value;
    }

    private static DateTime RequireUnixSeconds(XElement parent, string name)
    {
        var text = RequireElement(parent, name);
        if (!long.TryParse(text, out var seconds))
        {
            throw new DmarcParseException($"<{name}> is not a valid Unix timestamp: '{text}'.");
        }

        return DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
    }
}
