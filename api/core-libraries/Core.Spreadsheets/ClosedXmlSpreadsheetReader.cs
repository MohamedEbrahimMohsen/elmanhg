using ClosedXML.Excel;
using Core.Errors;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.IO.Packaging;

namespace Core.Spreadsheets;

public sealed class ClosedXmlSpreadsheetReader(IOptions<SpreadsheetOptions> spreadsheetOptions) : ISpreadsheetReader
{
    // Row 1 holds the column keys; data rows start at Excel row 2.
    private const int HeaderRow = 1;

    public SpreadsheetWorkbook Read(Stream content, SpreadsheetReadLimits limits)
    {
        using var buffered = content.CanSeek ? null : Buffer(content);
        var package = buffered ?? content;
        SpreadsheetPackageGuard.EnsureWithinUncompressedCap(package, spreadsheetOptions.Value.MaxUncompressedSizeInMb, spreadsheetOptions.Value.UnreadableErrorCode);
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(package);
        }
        catch (Exception exception) when (exception is InvalidDataException or FileFormatException or OpenXmlPackageException or ArgumentException or InvalidOperationException)
        {
            throw new BadRequestCoreException(spreadsheetOptions.Value.UnreadableErrorCode, innerException: exception);
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

    private static MemoryStream Buffer(Stream content)
    {
        var buffer = new MemoryStream();
        content.CopyTo(buffer);
        buffer.Position = 0;
        return buffer;
    }

    private static SpreadsheetSheet ReadSheet(IXLWorksheet worksheet, int maxColumns, int maxRows)
    {
        var headers = ReadCells(worksheet, HeaderRow, maxColumns);
        var rows = worksheet.RowsUsed()
            .Select(x => x.RowNumber())
            .Where(x => x > HeaderRow)
            .Select(x => new SpreadsheetRow(x, ReadCells(worksheet, x, headers.Count)))
            .Where(x => x.Cells.Any(cell => cell.Length > 0))
            .Take(maxRows)
            .ToList();
        return new SpreadsheetSheet(worksheet.Name, headers, rows);
    }

    private static List<string> ReadCells(IXLWorksheet worksheet, int row, int maxColumns)
    {
        if (maxColumns == 0)
        {
            return [];
        }

        var used = worksheet.Range(row, 1, row, maxColumns).CellsUsed()
            .Select(x => (Column: x.Address.ColumnNumber, Text: CellText(x)))
            .ToList();
        var cells = Enumerable.Repeat(string.Empty, used.Count == 0 ? 0 : used.Max(x => x.Column)).ToList();
        foreach (var (column, text) in used)
        {
            cells[column - 1] = text;
        }

        return cells;
    }

    private static string CellText(IXLCell cell)
    {
        var text = cell.DataType switch
        {
            XLDataType.Blank => string.Empty,
            XLDataType.Boolean => cell.GetBoolean() ? bool.TrueString.ToLowerInvariant() : bool.FalseString.ToLowerInvariant(),
            XLDataType.Number => cell.GetDouble().ToString(CultureInfo.InvariantCulture),
            XLDataType.DateTime => cell.GetDateTime().ToString("O", CultureInfo.InvariantCulture),
            XLDataType.TimeSpan => cell.GetTimeSpan().ToString("c", CultureInfo.InvariantCulture),
            XLDataType.Error => string.Empty,
            _ => cell.GetString(),
        };
        return text.Trim();
    }
}
