using System.IO.Compression;
using DmarcAnalyzer.Core.Dmarc;
using DmarcAnalyzer.Core.Entities;

namespace DmarcAnalyzer.Infrastructure.Ingestion;

public record ExtractedXmlAttachment(AttachmentType Type, string SourceFileName, Stream XmlContent);

/// <summary>
/// Detects and unwraps a DMARC RUA attachment: receivers commonly send a .zip or .gz containing the
/// report XML, and a few send the raw .xml directly. RUF (forensic) attachments are far rarer and are
/// passed through as-is — no deep parsing in MVP (see DmarcAnalyzer.Core.Dmarc for why).
/// </summary>
public static class AttachmentExtractor
{
    // The shared mailbox receives attachments from arbitrary reporting organizations on the internet —
    // this is untrusted, attacker-reachable input. A tiny, highly-compressed zip/gzip "report" could
    // otherwise decompress to gigabytes and exhaust memory (a decompression bomb). A real RUA/RUF
    // report XML is at most a few MB even for a very high-volume domain, so 50 MB is generous headroom
    // while still bounding the DoS. Enforced by counting actual bytes copied, not by trusting the
    // archive's declared (attacker-controlled) entry size.
    private const long MaxDecompressedBytes = 50 * 1024 * 1024;

    public static ExtractedXmlAttachment? TryExtractXml(string fileName, byte[] contentBytes)
    {
        var lowerName = fileName.ToLowerInvariant();

        if (lowerName.EndsWith(".zip"))
        {
            using var zipStream = new MemoryStream(contentBytes);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
            var xmlEntry = archive.Entries.FirstOrDefault(e => e.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
            if (xmlEntry is null)
            {
                return null;
            }

            var buffer = new MemoryStream();
            using (var entryStream = xmlEntry.Open())
            {
                CopyWithLimit(entryStream, buffer, MaxDecompressedBytes);
            }
            buffer.Position = 0;
            return new ExtractedXmlAttachment(AttachmentType.RuaZip, xmlEntry.Name, buffer);
        }

        if (lowerName.EndsWith(".gz"))
        {
            var buffer = new MemoryStream();
            using (var compressedStream = new MemoryStream(contentBytes))
            using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
            {
                CopyWithLimit(gzipStream, buffer, MaxDecompressedBytes);
            }
            buffer.Position = 0;
            return new ExtractedXmlAttachment(AttachmentType.RuaGzip, fileName, buffer);
        }

        if (lowerName.EndsWith(".xml"))
        {
            return new ExtractedXmlAttachment(AttachmentType.RuaXml, fileName, new MemoryStream(contentBytes));
        }

        return null;
    }

    private static void CopyWithLimit(Stream source, Stream destination, long maxBytes)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                throw new DmarcParseException($"Attachment decompresses to more than the {maxBytes:N0}-byte limit — refusing to process it.");
            }

            destination.Write(buffer, 0, read);
        }
    }
}
