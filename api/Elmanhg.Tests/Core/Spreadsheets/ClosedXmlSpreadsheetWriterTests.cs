using ClosedXML.Excel;
using Core.Spreadsheets;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Core.Spreadsheets;

public sealed class ClosedXmlSpreadsheetWriterTests
{
    private readonly ClosedXmlSpreadsheetWriter _writer = new(Options.Create(new SpreadsheetOptions { RightToLeft = true }));

    [Fact]
    public void Write_Definitions_RoundTripsThroughReader()
    {
        var content = _writer.Write([new SpreadsheetSheetDefinition("Instructions", [new SpreadsheetColumn("sheet", [], 20), new SpreadsheetColumn("description", [], 90)], [["Mcq", "Question text"]]), Difficulty()]);

        var workbook = new ClosedXmlSpreadsheetReader(Options.Create(new SpreadsheetOptions())).Read(new MemoryStream(content), new SpreadsheetReadLimits(_ => true, 256, 500));

        workbook.Sheets.Select(x => x.Name).Should().Equal("Instructions", "Mcq");
        workbook.Sheets[0].Headers.Should().Equal("sheet", "description");
        workbook.Sheets[0].Rows.Single().Cells.Should().Equal("Mcq", "Question text");
        workbook.Sheets[1].Headers.Should().Equal("stem", "difficulty");
    }

    [Fact]
    public void Write_ColumnWithAllowedValues_AddsListValidation()
    {
        var content = _writer.Write([Difficulty()]);

        using var workbook = new XLWorkbook(new MemoryStream(content));
        workbook.Worksheet("Mcq").DataValidations.Should().ContainSingle().Which.MinValue.Should().Contain("easy,medium,hard");
    }

    [Fact]
    public void Write_Definition_IsRightToLeftWithFrozenHeader()
    {
        var content = _writer.Write([Difficulty()]);

        using var workbook = new XLWorkbook(new MemoryStream(content));
        var sheet = workbook.Worksheet("Mcq");
        sheet.RightToLeft.Should().BeTrue();
        sheet.SheetView.SplitRow.Should().Be(1);
    }

    [Fact]
    public void Write_RightToLeftOff_IsLeftToRight()
    {
        var content = new ClosedXmlSpreadsheetWriter(Options.Create(new SpreadsheetOptions())).Write([Difficulty()]);

        using var workbook = new XLWorkbook(new MemoryStream(content));
        workbook.Worksheet(1).RightToLeft.Should().BeFalse();
    }

    private static SpreadsheetSheetDefinition Difficulty() => new("Mcq", [new SpreadsheetColumn("stem", [], 24), new SpreadsheetColumn("difficulty", ["easy", "medium", "hard"], 24)], []);
}
