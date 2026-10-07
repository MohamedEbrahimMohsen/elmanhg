using ClosedXML.Excel;
using Core.Spreadsheets;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Core.Spreadsheets;

public sealed class ClosedXmlSpreadsheetReaderTests
{
    private static readonly SpreadsheetReadLimits AllSheets = new(_ => true, 256, 500);

    private readonly ClosedXmlSpreadsheetReader _reader = new(Options.Create(new SpreadsheetOptions { UnreadableErrorCode = "PROBE_UNREADABLE" }));

    [Fact]
    public void Read_Workbook_ReturnsSheetsHeadersAndRowsWithExcelRowNumbers()
    {
        var content = new QuestionWorkbookBuilder().Sheet("Mcq", ["stem", "correct"], ["one", "a"], ["two", "b"]).Build();

        var sheet = _reader.Read(new MemoryStream(content), AllSheets).Sheets.Single();

        sheet.Name.Should().Be("Mcq");
        sheet.Headers.Should().Equal("stem", "correct");
        sheet.Rows.Select(x => x.Number).Should().Equal(2, 3);
        sheet.Rows[1].Cells.Should().Equal("two", "b");
    }

    [Fact]
    public void Read_BlankRow_IsSkipped()
    {
        var content = new QuestionWorkbookBuilder().Sheet("Mcq", ["stem"], ["one"], [""], ["three"]).Build();

        var sheet = _reader.Read(new MemoryStream(content), AllSheets).Sheets.Single();

        sheet.Rows.Select(x => x.Number).Should().Equal(2, 4);
    }

    [Fact]
    public void Read_NumberAndBooleanCells_UseInvariantText()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Short");
        sheet.Cell(1, 1).Value = "value";
        sheet.Cell(1, 2).Value = "max_score";
        sheet.Cell(1, 3).Value = "unify_letter_variants";
        sheet.Cell(2, 1).Value = 9.8;
        sheet.Cell(2, 2).Value = 2;
        sheet.Cell(2, 3).Value = true;
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var row = _reader.Read(stream, AllSheets).Sheets.Single().Rows.Single();

        row.Cells.Should().Equal("9.8", "2", "true");
    }
}
