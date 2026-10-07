using Core.Errors;
using DocumentFormat.OpenXml.Packaging;
using System.IO.Compression;
using System.IO.Packaging;

namespace Core.Spreadsheets;

// ClosedXML unpacks every part into memory before any row or column cap applies; the declared sizes are checked first. System.IO.Compression stops an entry at its declared size.
internal static class SpreadsheetPackageGuard
{
    private const long BytesPerMegabyte = 1024 * 1024;

    public static void EnsureWithinUncompressedCap(Stream package, int maxUncompressedSizeInMb, string unreadableErrorCode)
    {
        var start = package.Position;
        var cap = maxUncompressedSizeInMb * BytesPerMegabyte;
        long total = 0;
        try
        {
            using var archive = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true);
            foreach (var entry in archive.Entries)
            {
                total += entry.Length;
                if (entry.Length > cap || total > cap)
                {
                    throw new BadRequestCoreException(unreadableErrorCode);
                }
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or FileFormatException or OpenXmlPackageException or ArgumentException or InvalidOperationException)
        {
            throw new BadRequestCoreException(unreadableErrorCode, innerException: exception);
        }

        package.Position = start;
    }
}
