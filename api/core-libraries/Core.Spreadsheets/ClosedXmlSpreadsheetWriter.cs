using ClosedXML.Excel;
using Microsoft.Extensions.Options;

namespace Core.Spreadsheets;

public sealed class ClosedXmlSpreadsheetWriter(IOptions<SpreadsheetOptions> spreadsheetOptions) : ISpreadsheetWriter
{
    // Row 1 holds the column keys; data rows start at Excel row 2.
    private const int HeaderRow = 1;

    public byte[] Write(IReadOnlyList<SpreadsheetSheetDefinition> sheets)
    {
        using var workbook = new XLWorkbook();
        foreach (var definition in sheets)
        {
            WriteSheet(workbook.Worksheets.Add(definition.Name), definition);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private void WriteSheet(IXLWorksheet sheet, SpreadsheetSheetDefinition definition)
    {
        sheet.RightToLeft = spreadsheetOptions.Value.RightToLeft;
        for (var index = 0; index < definition.Columns.Count; index++)
        {
            var column = definition.Columns[index];
            var number = index + 1;
            var header = sheet.Cell(HeaderRow, number);
            header.Value = column.Header;
            header.Style.Font.Bold = true;
            sheet.Column(number).Width = column.Width;
            if (column.AllowedValues.Count > 0)
            {
                sheet.Range(HeaderRow + 1, number, XLHelper.MaxRowNumber, number).CreateDataValidation().List($"\"{string.Join(',', column.AllowedValues)}\"", true);
            }
        }

        for (var rowIndex = 0; rowIndex < definition.Rows.Count; rowIndex++)
        {
            var values = definition.Rows[rowIndex];
            for (var index = 0; index < values.Count; index++)
            {
                sheet.Cell(HeaderRow + 1 + rowIndex, index + 1).Value = values[index];
            }
        }

        sheet.SheetView.FreezeRows(HeaderRow);
    }
}
