using ClosedXML.Excel;
using Core.Errors;
using Core.Spreadsheets;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using System.Buffers.Binary;
using System.Text;

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

    [Fact]
    public void Read_NotASpreadsheet_ThrowsConfiguredUnreadableCode()
    {
        var act = () => _reader.Read(new MemoryStream(Encoding.UTF8.GetBytes("hello")), AllSheets);

        act.Should().Throw<BadRequestCoreException>().Which.ErrorCode.Should().Be("PROBE_UNREADABLE");
    }

    [Theory]
    [InlineData("truncated")]
    [InlineData("central-directory-offset")]
    [InlineData("entry-count")]
    [InlineData("disk-number")]
    [InlineData("local-header-offset")]
    [InlineData("entry-name-length")]
    public void Read_MalformedZip_ThrowsConfiguredUnreadableCode(string corruption)
    {
        var content = Corrupt(new QuestionWorkbookBuilder().Sheet("Mcq", ["stem"], ["one"]).Build(), corruption);

        var act = () => _reader.Read(new MemoryStream(content), AllSheets);

        act.Should().Throw<BadRequestCoreException>().Which.ErrorCode.Should().Be("PROBE_UNREADABLE");
    }

    [Fact]
    public void Read_NotASpreadsheetWithDefaultOptions_ThrowsSpreadsheetUnreadable()
    {
        var reader = new ClosedXmlSpreadsheetReader(Options.Create(new SpreadsheetOptions()));

        var act = () => reader.Read(new MemoryStream(Encoding.UTF8.GetBytes("hello")), AllSheets);

        act.Should().Throw<BadRequestCoreException>().Which.ErrorCode.Should().Be("SPREADSHEET_UNREADABLE");
    }

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

    [Fact]
    public void Read_UncompressedSizeOverCap_ThrowsConfiguredUnreadableCode()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Mcq");
        for (var i = 1; i <= 64; i++)
        {
            sheet.Cell(i, 1).Value = new string('a', 32_000) + i;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var act = () => OneMegabyteCapReader().Read(stream, AllSheets);

        act.Should().Throw<BadRequestCoreException>().Which.ErrorCode.Should().Be("PROBE_UNREADABLE");
    }

    [Fact]
    public void Read_UncompressedSizeWithinCap_ReadsWorkbook()
    {
        var content = new QuestionWorkbookBuilder().Sheet("Mcq", ["stem", "correct"], ["one", "a"], ["two", "b"]).Build();

        var sheet = OneMegabyteCapReader().Read(new MemoryStream(content), AllSheets).Sheets.Single();

        sheet.Name.Should().Be("Mcq");
        sheet.Rows.Select(x => x.Number).Should().Equal(2, 3);
    }

    [Fact]
    public void Read_NonSeekableStream_ReadsWorkbook()
    {
        var content = new QuestionWorkbookBuilder().Sheet("Mcq", ["stem", "correct"], ["one", "a"], ["two", "b"]).Build();
        using var stream = new NonSeekableReadStream(content);

        var sheet = _reader.Read(stream, AllSheets).Sheets.Single();

        sheet.Headers.Should().Equal("stem", "correct");
        sheet.Rows.Select(x => x.Number).Should().Equal(2, 3);
        sheet.Rows[1].Cells.Should().Equal("two", "b");
    }

    [Fact]
    public void Read_NonSeekableStreamOverCompressedCap_ThrowsConfiguredUnreadableCode()
    {
        var content = IncompressibleWorkbook();
        var reader = new ClosedXmlSpreadsheetReader(Options.Create(new SpreadsheetOptions { UnreadableErrorCode = "PROBE_UNREADABLE", MaxCompressedSizeInMb = 1 }));
        using var stream = new NonSeekableReadStream(content);

        var act = () => reader.Read(stream, AllSheets);

        content.Length.Should().BeGreaterThan(1024 * 1024);
        act.Should().Throw<BadRequestCoreException>().Which.ErrorCode.Should().Be("PROBE_UNREADABLE");
        reader.Read(new MemoryStream(content), AllSheets).Sheets.Single().Name.Should().Be("Mcq");
    }

    private static ClosedXmlSpreadsheetReader OneMegabyteCapReader() => new(Options.Create(new SpreadsheetOptions { UnreadableErrorCode = "PROBE_UNREADABLE", MaxUncompressedSizeInMb = 1 }));

    private static byte[] Corrupt(byte[] content, string corruption)
    {
        const int endRecordLength = 22;
        var end = content.Length - endRecordLength;
        var centralDirectory = (int)BinaryPrimitives.ReadUInt32LittleEndian(content.AsSpan(end + 16));
        if (corruption == "truncated")
        {
            return content[..(content.Length / 2)];
        }

        var (offset, value, width) = corruption switch
        {
            "central-directory-offset" => (end + 16, 0x7FFFFFF0u, 4),
            "entry-count" => (end + 10, 0xFFFFu, 2),
            "disk-number" => (end + 4, 1u, 2),
            "local-header-offset" => (centralDirectory + 42, 0x7FFFFFF0u, 4),
            _ => (centralDirectory + 28, 0xFFFFu, 2),
        };
        BitConverter.GetBytes(value)[..width].CopyTo(content, offset);
        return content;
    }

    private static byte[] IncompressibleWorkbook()
    {
        var random = new Random(331);
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Mcq");
        sheet.Cell(1, 1).Value = "stem";
        for (var i = 2; i <= 65; i++)
        {
            var bytes = new byte[24_000];
            random.NextBytes(bytes);
            sheet.Cell(i, 1).Value = Convert.ToBase64String(bytes);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
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
