using System.IO.Compression;
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
                entryStream.CopyTo(buffer);
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
                gzipStream.CopyTo(buffer);
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
}
