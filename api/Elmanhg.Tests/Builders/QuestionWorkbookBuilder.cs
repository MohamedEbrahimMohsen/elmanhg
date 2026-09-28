using ClosedXML.Excel;

namespace Elmanhg.Tests.Builders;

public sealed class QuestionWorkbookBuilder
{
    private readonly List<(string Name, string[] Headers, string[][] Rows)> _sheets = [];

    public QuestionWorkbookBuilder Sheet(string name, string[] headers, params string[][] rows)
    {
        _sheets.Add((name, headers, rows));
        return this;
    }

    public byte[] Build()
    {
        using var workbook = new XLWorkbook();
        foreach (var (name, headers, rows) in _sheets)
        {
            var sheet = workbook.Worksheets.Add(name);
            for (var column = 0; column < headers.Length; column++)
            {
                sheet.Cell(1, column + 1).Value = headers[column];
            }

            for (var row = 0; row < rows.Length; row++)
            {
                for (var column = 0; column < rows[row].Length; column++)
                {
                    sheet.Cell(row + 2, column + 1).Value = rows[row][column];
                }
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
