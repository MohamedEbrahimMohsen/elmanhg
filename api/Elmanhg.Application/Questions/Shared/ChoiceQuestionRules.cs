using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.RichText;
using Elmanhg.Domain.Questions.Schemas;
using System.Text.Json;

namespace Elmanhg.Application.Questions.Shared;

public static class ChoiceQuestionRules
{
    // A choice question with fewer than two options offers no choice.
    private const int MinimumOptions = 2;

    public static List<string> Validate(JsonElement body, JsonElement gradingSpec, bool multiple, ContentOptions options)
    {
        List<string> errors = [];
        var bodyRead = QuestionSchemaReader.TryRead<ChoiceBody>(body, out var choiceBody);
        McqGradingSpec? mcqSpec = null;
        MultiGradingSpec? multiSpec = null;
        var specRead = multiple ? QuestionSchemaReader.TryRead(gradingSpec, out multiSpec) : QuestionSchemaReader.TryRead(gradingSpec, out mcqSpec);
        if (!bodyRead)
        {
            errors.Add(ErrorCodes.QuestionBodyInvalid);
        }

        if (!specRead)
        {
            errors.Add(ErrorCodes.QuestionGradingSpecInvalid);
        }

        if (!bodyRead || !specRead)
        {
            return errors;
        }

        var choices = choiceBody!.Options ?? [];
        var ids = choices.Select(x => x?.Id).ToList();
        QuestionSchemaReader.AddIf(errors, choices.Count < MinimumOptions || choices.Count > options.QuestionOptionsMaxCount, ErrorCodes.QuestionOptionsCountInvalid);
        QuestionSchemaReader.AddIf(errors, ids.Any(x => !QuestionSchemaReader.IsValidId(x)), ErrorCodes.QuestionOptionIdInvalid);
        QuestionSchemaReader.AddIf(errors, ids.Distinct(StringComparer.Ordinal).Count() != ids.Count, ErrorCodes.QuestionOptionIdDuplicate);
        QuestionSchemaReader.AddIf(errors, choices.Any(x => string.IsNullOrWhiteSpace(x?.Text)), ErrorCodes.QuestionOptionTextRequired);
        QuestionSchemaReader.AddIf(errors, choices.Any(x => x?.Text is not null && x.Text.Length > options.QuestionOptionTextMaxLength), ErrorCodes.QuestionOptionTextTooLong);
        var correctInvalid = multiple
            ? multiSpec!.CorrectOptionIds is not { Count: > 0 } || multiSpec.CorrectOptionIds.Any(x => !ids.Contains(x))
            : mcqSpec!.CorrectOptionId is null || !ids.Contains(mcqSpec.CorrectOptionId);
        QuestionSchemaReader.AddIf(errors, correctInvalid, ErrorCodes.QuestionCorrectOptionInvalid);
        return errors;
    }

    public static (string Body, string GradingSpec) Normalize(JsonElement body, JsonElement gradingSpec, bool multiple, IRichTextSanitizer sanitizer)
    {
        var read = QuestionSchemaReader.Read<ChoiceBody>(body);
        var normalizedBody = new ChoiceBody(read.Options!
            .Select(x => new ChoiceOption(x.Id, sanitizer.Sanitize(x.Text)))
            .ToList());
        var normalizedSpec = multiple
            ? QuestionSchemaReader.Serialize(NormalizeMulti(QuestionSchemaReader.Read<MultiGradingSpec>(gradingSpec)))
            : QuestionSchemaReader.Serialize(new McqGradingSpec(QuestionSchemaReader.Read<McqGradingSpec>(gradingSpec).CorrectOptionId));
        return (QuestionSchemaReader.Serialize(normalizedBody), normalizedSpec);
    }

    private static MultiGradingSpec NormalizeMulti(MultiGradingSpec spec)
    {
        return new MultiGradingSpec(spec.CorrectOptionIds!.Distinct(StringComparer.Ordinal).ToList(), spec.PartialCredit);
    }
}
