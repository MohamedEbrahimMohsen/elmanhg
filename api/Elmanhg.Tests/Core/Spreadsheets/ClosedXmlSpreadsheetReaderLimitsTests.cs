using ClosedXML.Excel;
using Core.Spreadsheets;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Core.Spreadsheets;

public sealed class ClosedXmlSpreadsheetReaderLimitsTests
{
    private static readonly SpreadsheetReadLimits AllSheets = new(_ => true, 256, 500);

    private readonly ClosedXmlSpreadsheetReader _reader = new(Options.Create(new SpreadsheetOptions { UnreadableErrorCode = "PROBE_UNREADABLE" }));

    [Fact(Timeout = 10000)]
    public async Task Read_FarCellAtLastColumnAndRow_FinishesFastAndReadsOnlyUpToTheHeader()
    {
        var content = FarCellWorkbook("Mcq", "Junk");
        var limits = new SpreadsheetReadLimits(x => x == "Mcq", 256, 500);

        var workbook = await Task.Run(() => _reader.Read(new MemoryStream(content), limits), TestContext.Current.CancellationToken);

        content.Length.Should().BeLessThan(20_000);
        var sheet = workbook.Sheets.Single();
        sheet.Name.Should().Be("Mcq");
        sheet.Headers.Should().Equal("stem");
        sheet.Rows.Should().ContainSingle().Which.Cells.Should().Equal("one");
    }

    [Fact(Timeout = 10000)]
    public async Task Read_FarCellInTheHeaderRow_CapsTheColumnsRead()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Mcq");
        sheet.Cell(1, 1).Value = "stem";
        sheet.Cell(1, XLHelper.MaxColumnNumber).Value = "far";
        sheet.Cell(2, 1).Value = "one";
        sheet.Cell(2, XLHelper.MaxColumnNumber).Value = "x";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var result = await Task.Run(() => _reader.Read(stream, new SpreadsheetReadLimits(_ => true, 3, 500)), TestContext.Current.CancellationToken);

        result.Sheets.Single().Headers.Should().Equal("stem");
        result.Sheets.Single().Rows.Single().Cells.Should().Equal("one");
    }

    [Fact]
    public void Read_MoreRowsThanTheCap_StopsOneRowPastTheCapAcrossSheets()
    {
        var content = new QuestionWorkbookBuilder().Sheet("Mcq", ["stem"], ["one"], ["two"]).Sheet("Multi", ["stem"], ["three"], ["four"]).Sheet("Short", ["stem"], ["five"]).Build();

        var workbook = _reader.Read(new MemoryStream(content), new SpreadsheetReadLimits(_ => true, 256, 2));

        workbook.Sheets.Select(x => x.Name).Should().Equal("Mcq", "Multi");
        workbook.Sheets.Sum(x => x.Rows.Count).Should().Be(3);
    }

    private static byte[] FarCellWorkbook(params string[] names)
    {
        using var workbook = new XLWorkbook();
        foreach (var name in names)
        {
            var sheet = workbook.Worksheets.Add(name);
            sheet.Cell(1, 1).Value = "stem";
            sheet.Cell(2, 1).Value = "one";
            sheet.Cell(XLHelper.MaxRowNumber, XLHelper.MaxColumnNumber).Value = "x";
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
