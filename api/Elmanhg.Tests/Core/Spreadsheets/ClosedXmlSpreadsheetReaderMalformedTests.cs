using Core.Errors;
using Core.Spreadsheets;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using System.Text;
using System.Xml;

namespace Elmanhg.Tests.Core.Spreadsheets;

public sealed class ClosedXmlSpreadsheetReaderMalformedTests
{
    private static readonly SpreadsheetReadLimits AllSheets = new(_ => true, 256, 500);

    private readonly ClosedXmlSpreadsheetReader _reader = new(Options.Create(new SpreadsheetOptions { UnreadableErrorCode = "PROBE_UNREADABLE" }));

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
        var content = SpreadsheetPackageCorruption.Corrupt(new QuestionWorkbookBuilder().Sheet("Mcq", ["stem"], ["one"]).Build(), corruption);

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

    [Theory]
    [InlineData("workbook-part-not-xml", typeof(XmlException))]
    [InlineData("content-types-not-xml", typeof(XmlException))]
    [InlineData("cell-reference-not-a1", typeof(FormatException))]
    [InlineData("workbook-part-missing", typeof(InvalidOperationException))]
    public void Read_MalformedWorkbookPart_ThrowsConfiguredUnreadableCode(string corruption, Type innerExceptionType)
    {
        var content = SpreadsheetPackageCorruption.CorruptPart(new QuestionWorkbookBuilder().Sheet("Mcq", ["stem"], ["one"]).Build(), corruption);

        var act = () => _reader.Read(new MemoryStream(content), AllSheets);

        var exception = act.Should().Throw<BadRequestCoreException>().Which;
        exception.ErrorCode.Should().Be("PROBE_UNREADABLE");
        exception.InnerException.Should().BeOfType(innerExceptionType);
    }
}
