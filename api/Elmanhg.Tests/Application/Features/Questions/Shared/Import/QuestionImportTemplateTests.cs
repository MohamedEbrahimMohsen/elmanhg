using Core.Localization;
using Elmanhg.Application.Questions.Shared.Import;
using Elmanhg.Domain.Questions;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Questions.Shared.Import;

public sealed class QuestionImportTemplateTests
{
    private readonly ILocalizer _localizer = Substitute.For<ILocalizer>();

    public QuestionImportTemplateTests()
    {
        _localizer.GetMessage(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Dictionary<string, object>?>()).Returns(x => $"L:{x.ArgAt<string?>(0)}");
    }

    [Fact]
    public void Build_Default_HasInstructionsThenOneSheetPerType()
    {
        var sheets = QuestionImportTemplate.Build(QuestionImportParserTests.ImportOptions(), _localizer);

        sheets.Select(x => x.Name).Should().Equal("Instructions", "Mcq", "Multi", "TrueFalse", "Fill", "Short");
    }

    [Fact]
    public void Build_ShortSheet_HeadersMatchImportColumns()
    {
        var options = QuestionImportParserTests.ImportOptions();

        var sheet = QuestionImportTemplate.Build(options, _localizer).Single(x => x.Name == "Short");

        sheet.Columns.Select(x => x.Header).Should().Equal(QuestionImportColumns.For(QuestionType.Short, options));
    }

    [Fact]
    public void Build_DifficultyColumn_OffersEasyMediumHard()
    {
        var sheet = QuestionImportTemplate.Build(QuestionImportParserTests.ImportOptions(), _localizer).Single(x => x.Name == "Mcq");

        sheet.Columns.Single(x => x.Header == "difficulty").AllowedValues.Should().Equal("easy", "medium", "hard");
    }

    [Fact]
    public void Build_Instructions_DescribesColumnsWithLocalizedHelp()
    {
        var instructions = QuestionImportTemplate.Build(QuestionImportParserTests.ImportOptions(), _localizer)[0];

        instructions.Rows[0][3].Should().Be("L:QUESTION_IMPORT_HELP_INTRO");
        var fillStem = instructions.Rows.Single(x => x[0] == "Fill" && x[1] == "stem");
        fillStem[2].Should().Be("yes");
        fillStem[3].Should().Be("L:QUESTION_IMPORT_HELP_STEM_FILL");
    }
}
