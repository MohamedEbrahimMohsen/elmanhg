using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.Shared.Import;
using Elmanhg.Application.Shared.Spreadsheets;
using Elmanhg.Domain.Questions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Questions.Shared.Import;

public sealed class QuestionImportCellsTests
{
    private const string Sheet = "Multi";
    private const string Column = "partial_credit";
    private const int RowNumber = 7;

    [Fact]
    public void Text_MissingColumn_ReturnsNull()
    {
        Cells("anything").Text("stem").Should().BeNull();
    }

    [Fact]
    public void Text_Whitespace_ReturnsNull()
    {
        Cells("   ").Text(Column).Should().BeNull();
    }

    [Fact]
    public void List_PipeSeparated_TrimsAndDropsEmpty()
    {
        Cells(" a | | b ").List(Column).Should().Equal("a", "b");
    }

    [Theory]
    [InlineData("true")]
    [InlineData("TRUE")]
    [InlineData("yes")]
    [InlineData("1")]
    public void Boolean_TrueToken_ReturnsTrue(string value)
    {
        var cells = Cells(value);

        cells.Boolean(Column).Should().BeTrue();
        cells.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("false")]
    [InlineData("No")]
    [InlineData("0")]
    public void Boolean_FalseToken_ReturnsFalse(string value)
    {
        Cells(value).Boolean(Column).Should().BeFalse();
    }

    [Fact]
    public void Boolean_Invalid_AddsCellInvalidWithColumnAndRow()
    {
        var cells = Cells("maybe");

        cells.Boolean(Column).Should().BeNull();
        cells.Errors.Should().Equal(new QuestionImportRowError(Sheet, RowNumber, Column, ErrorCodes.QuestionImportCellInvalid));
    }

    [Fact]
    public void Decimal_InvariantNumber_Parses()
    {
        Cells("9.8").Decimal(Column).Should().Be(9.8m);
    }

    [Fact]
    public void Decimal_Invalid_AddsCellInvalid()
    {
        var cells = Cells("9,8kg");

        cells.Decimal(Column).Should().BeNull();
        cells.Errors.Should().ContainSingle(x => x.Code == ErrorCodes.QuestionImportCellInvalid);
    }

    [Fact]
    public void Integer_Invalid_AddsCellInvalid()
    {
        var cells = Cells("1.5");

        cells.Integer(Column).Should().BeNull();
        cells.Errors.Should().ContainSingle(x => x.Code == ErrorCodes.QuestionImportCellInvalid);
    }

    [Fact]
    public void Enum_NameCaseInsensitive_Parses()
    {
        Cells("HARD").Enum<QuestionDifficulty>(Column).Should().Be(QuestionDifficulty.Hard);
    }

    [Fact]
    public void Enum_NumericText_AddsCellInvalid()
    {
        var cells = Cells("1");

        cells.Enum<QuestionDifficulty>(Column).Should().BeNull();
        cells.Errors.Should().ContainSingle(x => x.Code == ErrorCodes.QuestionImportCellInvalid);
    }

    private static QuestionImportCells Cells(string value)
    {
        return new QuestionImportCells(Sheet, new SpreadsheetRow(RowNumber, ["ignored", value]), new Dictionary<string, int> { ["other"] = 0, [Column] = 1 });
    }
}
