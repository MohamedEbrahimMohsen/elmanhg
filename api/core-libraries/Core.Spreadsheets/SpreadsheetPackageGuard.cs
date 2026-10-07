using Core.Errors;
using DocumentFormat.OpenXml.Packaging;
using System.IO.Compression;
using System.Xml;

namespace Core.Spreadsheets;

// ClosedXML unpacks every part into memory before any row or column cap applies; the declared sizes are checked first. System.IO.Compression stops an entry at its declared size.
internal static class SpreadsheetPackageGuard
{
    private const long BytesPerMegabyte = 1024 * 1024;

    private const int CopyChunkSize = 81920;

    // A non-seekable stream has to be copied before the zip directory can be read; the copy stops at the compressed cap.
    public static MemoryStream BufferWithinCompressedCap(Stream content, int maxCompressedSizeInMb, string unreadableErrorCode)
    {
        var cap = maxCompressedSizeInMb * BytesPerMegabyte;
        var buffer = new MemoryStream();
        var chunk = new byte[CopyChunkSize];
        int read;
        while ((read = content.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + read > cap)
            {
                buffer.Dispose();
                throw new BadRequestCoreException(unreadableErrorCode);
            }

            buffer.Write(chunk, 0, read);
        }

        buffer.Position = 0;
        return buffer;
    }

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
        catch (Exception exception) when (IsUnreadablePackage(exception))
        {
            throw new BadRequestCoreException(unreadableErrorCode, innerException: exception);
        }

        package.Position = start;
    }

    public static bool IsUnreadablePackage(Exception exception) => exception is InvalidDataException or FormatException or XmlException or OpenXmlPackageException or ArgumentException or InvalidOperationException;
}
