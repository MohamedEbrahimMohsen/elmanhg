using ClosedXML.Excel;
using Core.Errors;
using Core.Spreadsheets;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Core.Spreadsheets;

public sealed class ClosedXmlSpreadsheetReaderPackageSizeTests
{
    private static readonly SpreadsheetReadLimits AllSheets = new(_ => true, 256, 500);

    private readonly ClosedXmlSpreadsheetReader _reader = new(Options.Create(new SpreadsheetOptions { UnreadableErrorCode = "PROBE_UNREADABLE" }));

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
}
