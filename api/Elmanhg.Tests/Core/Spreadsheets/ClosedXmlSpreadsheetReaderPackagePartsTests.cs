using Core.Errors;
using Core.Spreadsheets;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Core.Spreadsheets;

public sealed class ClosedXmlSpreadsheetReaderPackagePartsTests
{
    private static readonly SpreadsheetReadLimits AllSheets = new(_ => true, 256, 500);

    private readonly ClosedXmlSpreadsheetReader _reader = new(Options.Create(new SpreadsheetOptions { UnreadableErrorCode = "PROBE_UNREADABLE" }));

    [Theory]
    [InlineData("content-types-missing", "The package part '[Content_Types].xml' is missing.")]
    [InlineData("package-relationships-missing", "The package part '_rels/.rels' is missing.")]
    [InlineData("workbook-relationship-missing", "The package has no workbook relationship.")]
    [InlineData("workbook-relationship-external", "The package has no workbook relationship.")]
    [InlineData("workbook-relationship-target-missing", "The package part 'xl/missing.xml' is missing.")]
    [InlineData("workbook-part-missing", "The package part 'xl/workbook.xml' is missing.")]
    [InlineData("workbook-relationships-missing", "The package part 'xl/_rels/workbook.xml.rels' is missing.")]
    [InlineData("sheet-relationship-missing", "A sheet in the workbook has no relationship.")]
    [InlineData("sheet-relationship-id-missing", "A sheet in the workbook has no relationship.")]
    [InlineData("sheet-part-missing", "The package part 'xl/worksheets/sheet1.xml' is missing.")]
    public void Read_RequiredPartMissing_ThrowsConfiguredUnreadableCode(string corruption, string missing)
    {
        var content = SpreadsheetPackageCorruption.CorruptPart(new QuestionWorkbookBuilder().Sheet("Mcq", ["stem"], ["one"]).Build(), corruption);

        var act = () => _reader.Read(new MemoryStream(content), AllSheets);

        var exception = act.Should().Throw<BadRequestCoreException>().Which;
        exception.ErrorCode.Should().Be("PROBE_UNREADABLE");
        exception.InnerException.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be(missing);
    }

    [Theory]
    [InlineData("relationship-targets-relative")]
    [InlineData("relationship-target-upper-case")]
    [InlineData("workbook-relationship-strict")]
    [InlineData("sheet-part-name-percent-encoded")]
    [InlineData("sheet-part-name-percent-encoded-literal-target")]
    [InlineData("sheet-part-name-arabic-percent-encoded")]
    [InlineData("sheet-part-name-arabic-percent-encoded-literal-target")]
    public void Read_ValidPackageVariant_ReadsWorkbook(string variant)
    {
        var content = SpreadsheetPackageCorruption.CorruptPart(new QuestionWorkbookBuilder().Sheet("Mcq", ["stem"], ["one"]).Build(), variant);

        var sheet = _reader.Read(new MemoryStream(content), AllSheets).Sheets.Single();

        sheet.Name.Should().Be("Mcq");
        sheet.Headers.Should().Equal("stem");
        sheet.Rows.Select(x => x.Number).Should().Equal(2);
        sheet.Rows[0].Cells.Should().Equal("one");
    }
}
