using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

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
        "content-types-missing" => RewriteEntry(workbook, "[Content_Types].xml", _ => null),
        "package-relationships-missing" => RewriteEntry(workbook, "_rels/.rels", _ => null),
        "workbook-relationship-missing" => RewriteEntry(workbook, "_rels/.rels", x => Regex.Replace(x, "<Relationship [^>]*/officeDocument\"[^>]*/>", string.Empty)),
        "workbook-relationship-external" => RewriteEntry(workbook, "_rels/.rels", x => x.Replace("Target=\"/xl/workbook.xml\"", "Target=\"/xl/workbook.xml\" TargetMode=\"External\"", StringComparison.Ordinal)),
        "workbook-relationship-target-missing" => RewriteEntry(workbook, "_rels/.rels", x => x.Replace("/xl/workbook.xml", "/xl/missing.xml", StringComparison.Ordinal)),
        "workbook-relationships-missing" => RewriteEntry(workbook, "xl/_rels/workbook.xml.rels", _ => null),
        "sheet-relationship-missing" => RewriteEntry(workbook, "xl/_rels/workbook.xml.rels", x => Regex.Replace(x, "<Relationship [^>]*/worksheets/sheet1\\.xml\"[^>]*/>", string.Empty)),
        "sheet-relationship-id-missing" => RewriteEntry(workbook, "xl/workbook.xml", x => Regex.Replace(x, " r:id=\"[^\"]*\"", string.Empty)),
        "sheet-part-missing" => RewriteEntry(workbook, "xl/worksheets/sheet1.xml", _ => null),
        "relationship-targets-relative" => RewriteEntry(RewriteEntry(workbook, "_rels/.rels", x => x.Replace("\"/xl/workbook.xml\"", "\"xl/workbook.xml\"", StringComparison.Ordinal)), "xl/_rels/workbook.xml.rels", x => x.Replace("\"/xl/", "\"", StringComparison.Ordinal)),
        "relationship-target-upper-case" => RewriteEntry(workbook, "_rels/.rels", x => x.Replace("/xl/workbook.xml", "/XL/Workbook.xml", StringComparison.Ordinal)),
        "workbook-relationship-strict" => RewriteEntry(workbook, "_rels/.rels", x => x.Replace("http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument", "http://purl.oclc.org/ooxml/officeDocument/relationships/officeDocument", StringComparison.Ordinal)),
        "sheet-part-name-percent-encoded" => RenameSheetPart(workbook, "xl/worksheets/my%20sheet.xml", "my%20sheet.xml"),
        "sheet-part-name-percent-encoded-literal-target" => RenameSheetPart(workbook, "xl/worksheets/my%20sheet.xml", "my sheet.xml"),
        "sheet-part-name-arabic-percent-encoded" => RenameSheetPart(workbook, "xl/worksheets/%D9%88.xml", "%D9%88.xml"),
        "sheet-part-name-arabic-percent-encoded-literal-target" => RenameSheetPart(workbook, "xl/worksheets/%D9%88.xml", "و.xml"),
        _ => throw new ArgumentOutOfRangeException(nameof(corruption)),
    };

    private static byte[] RenameSheetPart(byte[] workbook, string partName, string target)
    {
        var renamed = RewriteEntry(workbook, "xl/worksheets/sheet1.xml", x => x, partName);
        var retargeted = RewriteEntry(renamed, "xl/_rels/workbook.xml.rels", x => x.Replace("\"/xl/worksheets/sheet1.xml\"", $"\"worksheets/{target}\"", StringComparison.Ordinal));
        return RewriteEntry(retargeted, "[Content_Types].xml", x => x.Replace("\"/xl/worksheets/sheet1.xml\"", $"\"/{partName}\"", StringComparison.Ordinal));
    }

    public static byte[] RewriteEntry(byte[] workbook, string entryName, Func<string, string?> rewrite, string? renameTo = null)
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
                    using var writer = new StreamWriter(target.CreateEntry(renameTo ?? entry.FullName).Open(), new UTF8Encoding(false));
                    writer.Write(text);
                }
            }
        }

        return output.ToArray();
    }
}
