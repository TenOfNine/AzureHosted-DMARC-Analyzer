using System.IO.Compression;
using System.Text;
using DmarcAnalyzer.Core.Dmarc;
using DmarcAnalyzer.Core.Entities;
using DmarcAnalyzer.Infrastructure.Ingestion;
using Xunit;

namespace DmarcAnalyzer.Tests.Dmarc;

public class AttachmentExtractorTests
{
    [Fact]
    public void TryExtractXml_ZipAttachment_ProducesEquivalentParsedReport()
    {
        var zipBytes = ZipXml("report.xml", DmarcXmlFixtures.ValidFull);

        var extracted = AttachmentExtractor.TryExtractXml("report.xml.zip", zipBytes);

        Assert.NotNull(extracted);
        Assert.Equal(AttachmentType.RuaZip, extracted!.Type);
        var parsed = DmarcXmlParser.Parse(extracted.XmlContent);
        Assert.Equal("abc123-report-001", parsed.Metadata.ReportId);
    }

    [Fact]
    public void TryExtractXml_GzipAttachment_ProducesEquivalentParsedReport()
    {
        var gzipBytes = GzipXml(DmarcXmlFixtures.ValidFull);

        var extracted = AttachmentExtractor.TryExtractXml("report.xml.gz", gzipBytes);

        Assert.NotNull(extracted);
        Assert.Equal(AttachmentType.RuaGzip, extracted!.Type);
        var parsed = DmarcXmlParser.Parse(extracted.XmlContent);
        Assert.Equal("abc123-report-001", parsed.Metadata.ReportId);
    }

    [Fact]
    public void TryExtractXml_RawXmlAttachment_ProducesEquivalentParsedReport()
    {
        var rawBytes = Encoding.UTF8.GetBytes(DmarcXmlFixtures.ValidFull);

        var extracted = AttachmentExtractor.TryExtractXml("report.xml", rawBytes);

        Assert.NotNull(extracted);
        Assert.Equal(AttachmentType.RuaXml, extracted!.Type);
        var parsed = DmarcXmlParser.Parse(extracted.XmlContent);
        Assert.Equal("abc123-report-001", parsed.Metadata.ReportId);
    }

    [Fact]
    public void TryExtractXml_UnsupportedAttachment_ReturnsNull()
    {
        var extracted = AttachmentExtractor.TryExtractXml("logo.png", [0x89, 0x50, 0x4E, 0x47]);

        Assert.Null(extracted);
    }

    // The shared mailbox receives attachments from arbitrary senders on the internet. A small,
    // highly-compressible payload (repeated bytes) that decompresses past the extractor's limit must
    // be rejected rather than exhausted into memory — this is the decompression-bomb guard.
    [Fact]
    public void TryExtractXml_ZipEntryExceedsDecompressionLimit_ThrowsDmarcParseException()
    {
        var zipBytes = ZipXml("report.xml", new string('A', 51 * 1024 * 1024));

        Assert.Throws<DmarcParseException>(() => AttachmentExtractor.TryExtractXml("report.xml.zip", zipBytes));
    }

    [Fact]
    public void TryExtractXml_GzipExceedsDecompressionLimit_ThrowsDmarcParseException()
    {
        var gzipBytes = GzipXml(new string('A', 51 * 1024 * 1024));

        Assert.Throws<DmarcParseException>(() => AttachmentExtractor.TryExtractXml("report.xml.gz", gzipBytes));
    }

    private static byte[] ZipXml(string entryName, string xml)
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry(entryName);
            using var entryStream = entry.Open();
            using var writer = new StreamWriter(entryStream, Encoding.UTF8);
            writer.Write(xml);
        }

        return memoryStream.ToArray();
    }

    private static byte[] GzipXml(string xml)
    {
        using var memoryStream = new MemoryStream();
        using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress, leaveOpen: true))
        {
            var bytes = Encoding.UTF8.GetBytes(xml);
            gzipStream.Write(bytes, 0, bytes.Length);
        }

        return memoryStream.ToArray();
    }
}
