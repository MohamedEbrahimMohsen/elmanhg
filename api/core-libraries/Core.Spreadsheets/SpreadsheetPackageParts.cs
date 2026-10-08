using System.IO.Compression;

namespace Core.Spreadsheets;

// ClosedXML throws NullReferenceException when [Content_Types].xml, _rels/.rels or the workbook relationship is missing, and reads a sheet without a relationship as empty, so the parts it needs are checked before it opens the package.
internal static class SpreadsheetPackageParts
{
    private const string ContentTypesPartName = "[Content_Types].xml";
    private const string PackageRelationshipsPartName = "_rels/.rels";
    private static readonly string[] OfficeDocumentTypes = ["http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument", "http://purl.oclc.org/ooxml/officeDocument/relationships/officeDocument"];
    private static readonly Uri PackageRoot = new("http://package/");

    public static void EnsurePresent(ZipArchive archive)
    {
        var entries = archive.Entries.ToLookup(x => x.FullName, StringComparer.OrdinalIgnoreCase);
        Require(entries, ContentTypesPartName);
        var workbookRelationship = OpcPartReader.ReadRelationships(Require(entries, PackageRelationshipsPartName))
            .FirstOrDefault(x => !x.IsExternal && OfficeDocumentTypes.Contains(x.Type, StringComparer.Ordinal))
            ?? throw new InvalidOperationException("The package has no workbook relationship.");
        var workbookPartName = ResolvePartName(string.Empty, workbookRelationship.Target);
        var sheetRelationshipIds = OpcPartReader.ReadSheetRelationshipIds(Require(entries, workbookPartName));
        var workbookRelationships = OpcPartReader.ReadRelationships(Require(entries, RelationshipsPartName(workbookPartName)))
            .Where(x => !x.IsExternal)
            .ToLookup(x => x.Id, StringComparer.Ordinal);
        foreach (var id in sheetRelationshipIds)
        {
            var relationship = (id is null ? null : workbookRelationships[id].FirstOrDefault()) ?? throw new InvalidOperationException("A sheet in the workbook has no relationship.");
            Require(entries, ResolvePartName(workbookPartName, relationship.Target));
        }
    }

    private static ZipArchiveEntry Require(ILookup<string, ZipArchiveEntry> entries, string partName) => entries[partName].FirstOrDefault() ?? throw new InvalidOperationException($"The package part '{partName}' is missing.");

    // Zip item names are the percent-encoded part names, so the resolved path stays escaped, as System.IO.Packaging looks it up.
    private static string ResolvePartName(string sourcePartName, string target) => new Uri(new Uri(PackageRoot, sourcePartName), target).AbsolutePath.TrimStart('/');

    private static string RelationshipsPartName(string partName)
    {
        var folderLength = partName.LastIndexOf('/') + 1;
        return $"{partName[..folderLength]}_rels/{partName[folderLength..]}.rels";
    }
}
