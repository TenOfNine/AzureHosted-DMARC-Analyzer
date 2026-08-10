using System.Text;
using DmarcAnalyzer.Core.Dmarc;
using Xunit;

namespace DmarcAnalyzer.Tests.Dmarc;

public class DmarcXmlParserTests
{
    [Fact]
    public void Parse_ValidFullReport_MapsAllFields()
    {
        var report = DmarcXmlParser.Parse(ToStream(DmarcXmlFixtures.ValidFull));

        Assert.Equal("abc123-report-001", report.Metadata.ReportId);
        Assert.Equal("Example Receiver Inc.", report.Metadata.OrgName);
        Assert.Equal("dmarc-noreply@example-receiver.com", report.Metadata.Email);
        Assert.Equal("https://example-receiver.com/dmarc/support", report.Metadata.ExtraContactInfo);
        Assert.Equal(new DateTime(2023, 11, 14, 22, 13, 20, DateTimeKind.Utc), report.Metadata.DateRangeBeginUtc);

        Assert.Equal("contoso.com", report.Policy.Domain);
        Assert.Equal("r", report.Policy.Adkim);
        Assert.Equal("s", report.Policy.Aspf);
        Assert.Equal("reject", report.Policy.P);
        Assert.Equal("reject", report.Policy.Sp);
        Assert.Equal(100, report.Policy.Pct);
        Assert.Equal("1", report.Policy.Fo);

        Assert.Equal(2, report.Records.Count);

        var first = report.Records[0];
        Assert.Equal("203.0.113.5", first.SourceIp);
        Assert.Equal(3, first.Count);
        Assert.Equal("none", first.Disposition);
        Assert.Equal("pass", first.PolicyEvaluatedDkim);
        Assert.Equal("pass", first.PolicyEvaluatedSpf);
        Assert.Equal(["local_policy"], first.PolicyOverrideReasons);
        Assert.Equal("contoso.com", first.HeaderFrom);
        Assert.Equal("contoso.com", first.EnvelopeFrom);
        Assert.Equal("recipient.example.com", first.EnvelopeTo);

        var dkim = Assert.Single(first.DkimResults);
        Assert.Equal("contoso.com", dkim.Domain);
        Assert.Equal("selector1", dkim.Selector);
        Assert.Equal("pass", dkim.Result);

        var spf = Assert.Single(first.SpfResults);
        Assert.Equal("mfrom", spf.Scope);
        Assert.Equal("pass", spf.Result);
    }

    [Fact]
    public void Parse_ValidMinimalReport_LeavesOptionalFieldsNull()
    {
        var report = DmarcXmlParser.Parse(ToStream(DmarcXmlFixtures.ValidMinimal));

        Assert.Null(report.Metadata.ExtraContactInfo);
        Assert.Null(report.Policy.Sp);
        Assert.Equal(100, report.Policy.Pct); // defaults to 100 when omitted
        Assert.Null(report.Policy.Fo);

        var record = Assert.Single(report.Records);
        Assert.Null(record.EnvelopeFrom);
        Assert.Null(record.EnvelopeTo);
        Assert.Null(record.PolicyOverrideReasons);

        var spf = Assert.Single(record.SpfResults);
        Assert.Null(spf.Scope);
        Assert.Empty(record.DkimResults);
    }

    [Fact]
    public void Parse_MalformedXml_ThrowsDmarcParseException()
    {
        Assert.Throws<DmarcParseException>(() => DmarcXmlParser.Parse(ToStream(DmarcXmlFixtures.MalformedXml)));
    }

    [Fact]
    public void Parse_MissingRequiredField_ThrowsDmarcParseException()
    {
        var ex = Assert.Throws<DmarcParseException>(() => DmarcXmlParser.Parse(ToStream(DmarcXmlFixtures.MissingRequiredField)));
        Assert.Contains("report_id", ex.Message);
    }

    [Fact]
    public void Parse_NonFeedbackRoot_ThrowsDmarcParseException()
    {
        const string xml = "<somethingElse><a>1</a></somethingElse>";
        Assert.Throws<DmarcParseException>(() => DmarcXmlParser.Parse(ToStream(xml)));
    }

    private static MemoryStream ToStream(string xml) => new(Encoding.UTF8.GetBytes(xml));
}
