using ClosedXML.Excel;
using System.Globalization;

namespace Core.Spreadsheets;

public sealed partial class ClosedXmlSpreadsheetReader
{
    // Row 1 holds the column keys; data rows start at Excel row 2.
    private const int HeaderRow = 1;

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
