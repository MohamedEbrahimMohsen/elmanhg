using ClosedXML.Excel;
using Core.Errors;
using Microsoft.Extensions.Options;

namespace Core.Spreadsheets;

public sealed partial class ClosedXmlSpreadsheetReader(IOptions<SpreadsheetOptions> spreadsheetOptions) : ISpreadsheetReader
{
    public SpreadsheetWorkbook Read(Stream content, SpreadsheetReadLimits limits)
    {
        var options = spreadsheetOptions.Value;
        using var buffered = content.CanSeek ? null : SpreadsheetPackageGuard.BufferWithinCompressedCap(content, options.MaxCompressedSizeInMb, options.UnreadableErrorCode);
        var package = buffered ?? content;
        SpreadsheetPackageGuard.EnsureReadablePackage(package, options.MaxUncompressedSizeInMb, options.UnreadableErrorCode);
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(package);
        }
        catch (Exception exception) when (SpreadsheetPackageGuard.IsUnreadablePackage(exception))
        {
            throw new BadRequestCoreException(options.UnreadableErrorCode, innerException: exception);
        }

        using (workbook)
        {
            // One row past the cap is kept so the caller can still tell "too many rows" apart from "exactly at the cap".
            var remaining = limits.MaxDataRows + 1;
            List<SpreadsheetSheet> sheets = [];
            foreach (var worksheet in workbook.Worksheets.Where(x => limits.IncludeSheet(x.Name)))
            {
                var sheet = ReadSheet(worksheet, limits.MaxColumns, remaining);
                sheets.Add(sheet);
                remaining -= sheet.Rows.Count;
                if (remaining == 0)
                {
                    break;
                }
            }

            return new SpreadsheetWorkbook(sheets);
        }
    }
}
