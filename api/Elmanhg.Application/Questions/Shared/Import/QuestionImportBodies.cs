using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Questions.Schemas;
using System.Text.Json;
using static Elmanhg.Application.Questions.Shared.Import.QuestionImportColumns;
using ToleranceModeKind = Elmanhg.Domain.Questions.Schemas.ToleranceMode;

namespace Elmanhg.Application.Questions.Shared.Import;

public static class QuestionImportBodies
{
    public static (JsonElement Body, JsonElement GradingSpec) ReadChoice(QuestionImportCells cells, bool multiple, ContentOptions options)
    {
        var choices = OptionIds(options)
            .Select(id => (Id: id, Text: cells.Text(Option(id))))
            .Where(x => x.Text is not null)
            .Select(x => new ChoiceOption(x.Id, QuestionImportText.ToHtml(x.Text)))
            .ToList();
        var correct = cells.List(Correct)
            .Select(x => x.ToLowerInvariant())
            .ToList();
        var body = ToJson(new ChoiceBody(choices));
        return multiple
            ? (body, ToJson(new MultiGradingSpec(correct, cells.Boolean(PartialCredit) ?? false)))
            : (body, ToJson(new McqGradingSpec(correct.Count == 1 ? correct[0] : null)));
    }

    public static (JsonElement Body, JsonElement GradingSpec) ReadTrueFalse(QuestionImportCells cells)
    {
        return (ToJson(new TrueFalseBody()), ToJson(new TrueFalseGradingSpec(cells.Boolean(CorrectAnswer))));
    }

    public static (JsonElement Body, JsonElement GradingSpec) ReadFill(QuestionImportCells cells, ContentOptions options)
    {
        var blanks = BlankIds(options)
            .Select(id => (Id: id, Answers: cells.List(Blank(id))))
            .Where(x => x.Answers.Count > 0)
            .ToList();
        var body = new FillBody(blanks.Select(x => new FillBlank(x.Id)).ToList());
        var spec = new FillGradingSpec(blanks.Select(x => new FillBlankAnswers(x.Id, x.Answers)).ToList(), LetterVariants(cells.Boolean(UnifyLetterVariants) ?? true));
        return (ToJson(body), ToJson(spec));
    }

    public static (JsonElement Body, JsonElement GradingSpec) ReadShort(QuestionImportCells cells)
    {
        var kind = cells.Enum<ShortAnswerKind>(AnswerKind);
        var spec = kind switch
        {
            ShortAnswerKind.Numeric => new ShortGradingSpec(cells.Decimal(Value), cells.Decimal(Tolerance) ?? 0, cells.Enum<ToleranceModeKind>(QuestionImportColumns.ToleranceMode) ?? ToleranceModeKind.Absolute, null, null),
            ShortAnswerKind.Text => new ShortGradingSpec(null, null, null, cells.List(AcceptedAnswers), LetterVariants(cells.Boolean(UnifyLetterVariants) ?? true)),
            _ => new ShortGradingSpec(null, null, null, null, null),
        };
        return (ToJson(new ShortBody(kind)), ToJson(spec));
    }

    private static AnswerNormalization LetterVariants(bool unify) => new(UnifyAlef: unify, UnifyTaaMarbuta: unify, UnifyAlefMaqsura: unify);

    private static JsonElement ToJson<T>(T value) => JsonSerializer.SerializeToElement(value, QuestionJson.SerializerOptions);
}
