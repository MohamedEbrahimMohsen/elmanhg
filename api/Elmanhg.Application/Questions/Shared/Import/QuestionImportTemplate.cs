using Core.Localization;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.Spreadsheets;
using Elmanhg.Domain.Questions;
using Columns = Elmanhg.Application.Questions.Shared.Import.QuestionImportColumns;
using Help = Elmanhg.Application.Questions.Shared.Import.QuestionImportHelpKeys;

namespace Elmanhg.Application.Questions.Shared.Import;

public static class QuestionImportTemplate
{
    public const string InstructionsSheetName = "Instructions";

    // Excel column widths in characters; presentation only.
    private const int TypeColumnWidth = 24;
    private const int InstructionsNarrowWidth = 20;
    private const int InstructionsDescriptionWidth = 90;

    public static IReadOnlyList<SpreadsheetSheetDefinition> Build(ContentOptions options, ILocalizer localizer)
    {
        IReadOnlyList<SpreadsheetColumn> instructionColumns = [new("sheet", [], InstructionsNarrowWidth), new("column", [], InstructionsNarrowWidth), new("required", [], InstructionsNarrowWidth), new("description", [], InstructionsDescriptionWidth)];
        IReadOnlyList<string> intro = [string.Empty, string.Empty, string.Empty, localizer.GetMessage(Help.Intro)];
        var columnRows = Columns.Types
            .SelectMany(type => Columns.For(type, options).Select(column => (IReadOnlyList<string>)[type.ToString(), column, Columns.IsRequired(type, column) ? "yes" : string.Empty, localizer.GetMessage(HelpKey(type, column))]))
            .ToList();
        var instructions = new SpreadsheetSheetDefinition(InstructionsSheetName, instructionColumns, [intro, .. columnRows]);
        var typeSheets = Columns.Types
            .Select(type => new SpreadsheetSheetDefinition(type.ToString(), Columns.For(type, options).Select(column => new SpreadsheetColumn(column, Columns.AllowedValues(column), TypeColumnWidth)).ToList(), []))
            .ToList();
        return [instructions, .. typeSheets];
    }

    private static string HelpKey(QuestionType type, string column)
    {
        return column switch
        {
            Columns.Stem => type == QuestionType.Fill ? Help.StemFill : Help.Stem,
            Columns.Correct => type == QuestionType.Multi ? Help.CorrectMulti : Help.CorrectMcq,
            Columns.Explanation => Help.Explanation,
            Columns.Difficulty => Help.Difficulty,
            Columns.MaxScore => Help.MaxScore,
            Columns.Objective => Help.Objective,
            Columns.Tags => Help.Tags,
            Columns.PartialCredit => Help.PartialCredit,
            Columns.CorrectAnswer => Help.CorrectAnswer,
            Columns.UnifyLetterVariants => Help.UnifyLetterVariants,
            Columns.AnswerKind => Help.AnswerKind,
            Columns.Value => Help.Value,
            Columns.Tolerance => Help.Tolerance,
            Columns.ToleranceMode => Help.ToleranceMode,
            Columns.AcceptedAnswers => Help.AcceptedAnswers,
            _ when column.StartsWith(Columns.Option(string.Empty), StringComparison.Ordinal) => Help.Option,
            _ when column.StartsWith(Columns.Blank(string.Empty), StringComparison.Ordinal) => Help.Blank,
            _ => throw new InvalidOperationException("Unsupported import column."),
        };
    }
}
