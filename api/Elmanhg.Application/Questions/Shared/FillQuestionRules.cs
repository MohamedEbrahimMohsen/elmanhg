using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Questions.Schemas;
using System.Text.Json;

namespace Elmanhg.Application.Questions.Shared;

public static class FillQuestionRules
{
    // Blanks are placed in the stem as [[id]]; the student view replaces each marker with an input.
    public static string Placeholder(string id) => $"[[{id}]]";

    public static List<string> Validate(string stem, JsonElement body, JsonElement gradingSpec, ContentOptions options)
    {
        List<string> errors = [];
        var bodyRead = QuestionSchemaReader.TryRead<FillBody>(body, out var fillBody);
        var specRead = QuestionSchemaReader.TryRead<FillGradingSpec>(gradingSpec, out var spec);
        QuestionSchemaReader.AddIf(errors, !bodyRead, ErrorCodes.QuestionBodyInvalid);
        QuestionSchemaReader.AddIf(errors, !specRead, ErrorCodes.QuestionGradingSpecInvalid);
        if (!bodyRead || !specRead)
        {
            return errors;
        }

        var blankIds = (fillBody!.Blanks ?? []).Select(x => x?.Id).ToList();
        QuestionSchemaReader.AddIf(errors, blankIds.Count < 1 || blankIds.Count > options.QuestionBlanksMaxCount, ErrorCodes.QuestionBlanksCountInvalid);
        QuestionSchemaReader.AddIf(errors, blankIds.Any(x => !QuestionSchemaReader.IsValidId(x)), ErrorCodes.QuestionBlankIdInvalid);
        QuestionSchemaReader.AddIf(errors, blankIds.Distinct(StringComparer.Ordinal).Count() != blankIds.Count, ErrorCodes.QuestionBlankIdDuplicate);
        var placeholderMissing = blankIds
            .Where(QuestionSchemaReader.IsValidId)
            .Any(x => CountOccurrences(stem, Placeholder(x!)) != 1);
        QuestionSchemaReader.AddIf(errors, placeholderMissing, ErrorCodes.QuestionBlankPlaceholderMissing);
        var answers = spec!.Blanks ?? [];
        var answerIds = answers.Select(x => x?.Id).ToList();
        var mismatch = answerIds.Distinct(StringComparer.Ordinal).Count() != answerIds.Count || !answerIds.ToHashSet(StringComparer.Ordinal).SetEquals(blankIds);
        QuestionSchemaReader.AddIf(errors, mismatch, ErrorCodes.QuestionBlankAnswersMismatch);
        QuestionSchemaReader.AddIf(errors, answers.Any(x => !QuestionSchemaReader.AreValidAcceptedAnswers(x?.AcceptedAnswers, options)), ErrorCodes.QuestionAcceptedAnswersInvalid);
        return errors;
    }

    public static (string Body, string GradingSpec) Normalize(JsonElement body, JsonElement gradingSpec)
    {
        var blanks = QuestionSchemaReader.Read<FillBody>(body).Blanks!;
        var spec = QuestionSchemaReader.Read<FillGradingSpec>(gradingSpec);
        var normalizedBody = new FillBody(blanks.Select(x => new FillBlank(x.Id)).ToList());
        var answers = blanks
            .Select(b => new FillBlankAnswers(b.Id, QuestionSchemaReader.TrimAnswers(spec.Blanks!.Single(a => a.Id == b.Id).AcceptedAnswers)))
            .ToList();
        return (QuestionSchemaReader.Serialize(normalizedBody), QuestionSchemaReader.Serialize(new FillGradingSpec(answers, spec.UnifyLetterVariants)));
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = text.IndexOf(value, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal);
        }

        return count;
    }
}
