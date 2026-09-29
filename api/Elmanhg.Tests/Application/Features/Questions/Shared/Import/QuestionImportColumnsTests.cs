using Elmanhg.Application.Questions.Shared.Import;
using Elmanhg.Domain.Questions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Questions.Shared.Import;

public sealed class QuestionImportColumnsTests
{
    [Fact]
    public void For_Mcq_ListsStemOptionsCorrectThenCommonTail()
    {
        var options = QuestionImportParserTests.ImportOptions();
        options.QuestionOptionsMaxCount = 4;

        QuestionImportColumns.For(QuestionType.Mcq, options).Should().Equal("stem", "option_a", "option_b", "option_c", "option_d", "correct", "explanation", "difficulty", "max_score", "objective", "tags");
    }

    [Fact]
    public void For_Fill_ListsBlankColumnsUpToCap()
    {
        var options = QuestionImportParserTests.ImportOptions();
        options.QuestionBlanksMaxCount = 3;

        var columns = QuestionImportColumns.For(QuestionType.Fill, options);

        columns.Should().Contain(["blank_1", "blank_2", "blank_3", "unify_letter_variants"]).And.NotContain("blank_4");
    }

    [Fact]
    public void TypeForSheet_CaseInsensitiveName_ReturnsType()
    {
        QuestionImportColumns.TypeForSheet(" truefalse ").Should().Be(QuestionType.TrueFalse);
    }

    [Theory]
    [InlineData("Instructions")]
    [InlineData("1")]
    public void TypeForSheet_UnknownOrNumeric_ReturnsNull(string name)
    {
        QuestionImportColumns.TypeForSheet(name).Should().BeNull();
    }

    [Fact]
    public void TypeForSheet_EssaySheet_ReturnsNull()
    {
        QuestionImportColumns.TypeForSheet("Essay").Should().BeNull();
    }
}
