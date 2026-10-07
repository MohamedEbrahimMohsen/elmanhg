using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Elmanhg.Tests.Core.Spreadsheets;

public static class SpreadsheetPackageCorruption
{
    public static byte[] Corrupt(byte[] content, string corruption)
    {
        const int endRecordLength = 22;
        var end = content.Length - endRecordLength;
        var centralDirectory = (int)BinaryPrimitives.ReadUInt32LittleEndian(content.AsSpan(end + 16));
        if (corruption == "truncated")
        {
            return content[..(content.Length / 2)];
        }

        var (offset, value, width) = corruption switch
        {
            "central-directory-offset" => (end + 16, 0x7FFFFFF0u, 4),
            "entry-count" => (end + 10, 0xFFFFu, 2),
            "disk-number" => (end + 4, 1u, 2),
            "local-header-offset" => (centralDirectory + 42, 0x7FFFFFF0u, 4),
            _ => (centralDirectory + 28, 0xFFFFu, 2),
        };
        BitConverter.GetBytes(value)[..width].CopyTo(content, offset);
        return content;
    }

    public static byte[] CorruptPart(byte[] workbook, string corruption) => corruption switch
    {
        "workbook-part-not-xml" => RewriteEntry(workbook, "xl/workbook.xml", _ => "<workbook"),
        "content-types-not-xml" => RewriteEntry(workbook, "[Content_Types].xml", _ => "<Types"),
        "cell-reference-not-a1" => RewriteEntry(workbook, "xl/worksheets/sheet1.xml", x => x.Replace("r=\"A2\"", "r=\"!!\"", StringComparison.Ordinal)),
        "workbook-part-missing" => RewriteEntry(workbook, "xl/workbook.xml", _ => null),
        _ => throw new ArgumentOutOfRangeException(nameof(corruption)),
    };

    public static byte[] RewriteEntry(byte[] workbook, string entryName, Func<string, string?> rewrite)
    {
        using var output = new MemoryStream();
        using (var source = new ZipArchive(new MemoryStream(workbook), ZipArchiveMode.Read))
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in source.Entries)
            {
                if (entry.FullName != entryName)
                {
                    using var from = entry.Open();
                    using var to = target.CreateEntry(entry.FullName).Open();
                    from.CopyTo(to);
                    continue;
                }

                using var reader = new StreamReader(entry.Open());
                if (rewrite(reader.ReadToEnd()) is { } text)
                {
                    using var writer = new StreamWriter(target.CreateEntry(entry.FullName).Open(), new UTF8Encoding(false));
                    writer.Write(text);
                }
            }
        }

        return output.ToArray();
    }
}
