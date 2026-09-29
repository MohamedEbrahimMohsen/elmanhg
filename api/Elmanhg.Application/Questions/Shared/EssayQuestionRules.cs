using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.RichText;
using Elmanhg.Domain.Questions.Schemas;
using System.Text.Json;

namespace Elmanhg.Application.Questions.Shared;

public static class EssayQuestionRules
{
    public static List<string> Validate(JsonElement body, JsonElement gradingSpec, ContentOptions options)
    {
        List<string> errors = [];
        var bodyRead = QuestionSchemaReader.TryRead<EssayBody>(body, out var essayBody);
        var specRead = QuestionSchemaReader.TryRead<EssayGradingSpec>(gradingSpec, out var spec);
        QuestionSchemaReader.AddIf(errors, !bodyRead, ErrorCodes.QuestionBodyInvalid);
        QuestionSchemaReader.AddIf(errors, !specRead, ErrorCodes.QuestionGradingSpecInvalid);
        if (!bodyRead || !specRead)
        {
            return errors;
        }

        QuestionSchemaReader.AddIf(errors, essayBody!.MaxWords is { } words && (words < 1 || words > options.QuestionEssayMaxWordsMax), ErrorCodes.QuestionEssayMaxWordsInvalid);
        EssayRubricRules.AddErrors(errors, spec!.Criteria, options);
        var answers = spec.ModelAnswers ?? [];
        QuestionSchemaReader.AddIf(errors, answers.Count < 1 || answers.Count > options.QuestionModelAnswersMaxCount, ErrorCodes.QuestionModelAnswersCountInvalid);
        QuestionSchemaReader.AddIf(errors, answers.Any(string.IsNullOrWhiteSpace), ErrorCodes.QuestionModelAnswerRequired);
        QuestionSchemaReader.AddIf(errors, answers.Any(x => x is not null && x.Length > options.QuestionModelAnswerMaxLength), ErrorCodes.QuestionModelAnswerTooLong);
        return errors;
    }

    public static (string Body, string GradingSpec) Normalize(JsonElement body, JsonElement gradingSpec, IRichTextSanitizer sanitizer)
    {
        var read = QuestionSchemaReader.Read<EssayBody>(body);
        var spec = QuestionSchemaReader.Read<EssayGradingSpec>(gradingSpec);
        var criteria = spec.Criteria!
            .Select(NormalizeCriterion)
            .ToList();
        var modelAnswers = spec.ModelAnswers!
            .Select(sanitizer.Sanitize)
            .ToList();
        return (QuestionSchemaReader.Serialize(new EssayBody(read.MaxWords)), QuestionSchemaReader.Serialize(new EssayGradingSpec(criteria, modelAnswers)));
    }

    private static RubricCriterion NormalizeCriterion(RubricCriterion criterion)
    {
        var levels = criterion.Levels!
            .OrderBy(x => x.Points)
            .Select(x => new RubricLevel(x.Points, x.Description!.Trim()))
            .ToList();
        return new RubricCriterion(criterion.Id, criterion.Title!.Trim(), string.IsNullOrWhiteSpace(criterion.Description) ? null : criterion.Description.Trim(), criterion.Points, levels);
    }
}
