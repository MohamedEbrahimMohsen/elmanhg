using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Questions.Schemas;
using System.Text.Json;

namespace Elmanhg.Application.Questions.Shared;

public static class ShortQuestionRules
{
    public static List<string> Validate(JsonElement body, JsonElement gradingSpec, ContentOptions options)
    {
        List<string> errors = [];
        var bodyRead = QuestionSchemaReader.TryRead<ShortBody>(body, out var shortBody);
        var specRead = QuestionSchemaReader.TryRead<ShortGradingSpec>(gradingSpec, out var spec);
        QuestionSchemaReader.AddIf(errors, !bodyRead, ErrorCodes.QuestionBodyInvalid);
        QuestionSchemaReader.AddIf(errors, !specRead, ErrorCodes.QuestionGradingSpecInvalid);
        if (!bodyRead || !specRead)
        {
            return errors;
        }

        if (shortBody!.AnswerKind is null)
        {
            errors.Add(ErrorCodes.QuestionAnswerKindRequired);
            return errors;
        }

        if (!Enum.IsDefined(shortBody.AnswerKind.Value))
        {
            errors.Add(ErrorCodes.QuestionBodyInvalid);
            return errors;
        }

        if (shortBody.AnswerKind == ShortAnswerKind.Numeric)
        {
            QuestionSchemaReader.AddIf(errors, spec!.Value is null, ErrorCodes.QuestionNumericValueRequired);
            QuestionSchemaReader.AddIf(errors, spec.Tolerance is null or < 0 || spec.ToleranceMode is null || !Enum.IsDefined(spec.ToleranceMode.Value), ErrorCodes.QuestionToleranceInvalid);
            return errors;
        }

        QuestionSchemaReader.AddIf(errors, !QuestionSchemaReader.AreValidAcceptedAnswers(spec!.AcceptedAnswers, options), ErrorCodes.QuestionAcceptedAnswersInvalid);
        return errors;
    }

    public static (string Body, string GradingSpec) Normalize(JsonElement body, JsonElement gradingSpec)
    {
        var kind = QuestionSchemaReader.Read<ShortBody>(body).AnswerKind;
        var spec = QuestionSchemaReader.Read<ShortGradingSpec>(gradingSpec);
        var normalizedSpec = kind == ShortAnswerKind.Numeric
            ? new ShortGradingSpec(spec.Value, spec.Tolerance, spec.ToleranceMode, null, null)
            : new ShortGradingSpec(null, null, null, QuestionSchemaReader.TrimAnswers(spec.AcceptedAnswers), spec.Normalization ?? AnswerNormalization.Default);
        return (QuestionSchemaReader.Serialize(new ShortBody(kind)), QuestionSchemaReader.Serialize(normalizedSpec));
    }
}
