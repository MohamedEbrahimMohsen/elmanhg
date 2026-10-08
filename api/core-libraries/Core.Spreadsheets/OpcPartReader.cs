using System.IO.Compression;
using System.Xml;

namespace Core.Spreadsheets;

internal static class OpcPartReader
{
    private static readonly string[] RelationshipNamespaces = ["http://schemas.openxmlformats.org/officeDocument/2006/relationships", "http://purl.oclc.org/ooxml/officeDocument/relationships"];

    public static IReadOnlyList<OpcRelationship> ReadRelationships(ZipArchiveEntry entry)
    {
        List<OpcRelationship> relationships = [];
        using var reader = Open(entry);
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "Relationship")
            {
                relationships.Add(new OpcRelationship(reader.GetAttribute("Id") ?? string.Empty, reader.GetAttribute("Type") ?? string.Empty, reader.GetAttribute("Target") ?? string.Empty, string.Equals(reader.GetAttribute("TargetMode"), "External", StringComparison.Ordinal)));
            }
        }

        return relationships;
    }

    // workbook > sheets > sheet; the relationship id is r:id in the transitional or the strict relationships namespace.
    public static IReadOnlyList<string?> ReadSheetRelationshipIds(ZipArchiveEntry entry)
    {
        List<string?> ids = [];
        using var reader = Open(entry);
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.Depth == 2 && reader.LocalName == "sheet")
            {
                ids.Add(RelationshipId(reader));
            }
        }

        return ids;
    }

    private static string? RelationshipId(XmlReader reader)
    {
        while (reader.MoveToNextAttribute())
        {
            if (reader.LocalName == "id" && RelationshipNamespaces.Contains(reader.NamespaceURI, StringComparer.Ordinal))
            {
                return reader.Value;
            }
        }

        return null;
    }

    private static XmlReader Open(ZipArchiveEntry entry) => XmlReader.Create(entry.Open(), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, CloseInput = true });
}
