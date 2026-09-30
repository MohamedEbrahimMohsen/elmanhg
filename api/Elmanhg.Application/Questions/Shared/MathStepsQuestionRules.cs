using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Questions.Schemas;
using System.Text.Json;

namespace Elmanhg.Application.Questions.Shared;

public static class MathStepsQuestionRules
{
    public static List<string> Validate(JsonElement body, JsonElement gradingSpec, ContentOptions options)
    {
        List<string> errors = [];
        var bodyRead = QuestionSchemaReader.TryRead<MathStepsBody>(body, out _);
        var specRead = QuestionSchemaReader.TryRead<MathStepsGradingSpec>(gradingSpec, out var spec);
        QuestionSchemaReader.AddIf(errors, !bodyRead, ErrorCodes.QuestionBodyInvalid);
        QuestionSchemaReader.AddIf(errors, !specRead, ErrorCodes.QuestionGradingSpecInvalid);
        if (!bodyRead || !specRead)
        {
            return errors;
        }

        QuestionSchemaReader.AddIf(errors, !QuestionSchemaReader.AreValidAcceptedAnswers(spec!.AcceptedAnswers, options), ErrorCodes.QuestionMathAnswersInvalid);
        QuestionSchemaReader.AddIf(errors, spec.Form is { } form && !Enum.IsDefined(form), ErrorCodes.QuestionMathFormInvalid);
        if (spec.Tolerance is not null || spec.ToleranceMode is not null)
        {
            QuestionSchemaReader.AddIf(errors, spec.Tolerance is null or < 0 || spec.ToleranceMode is null || !Enum.IsDefined(spec.ToleranceMode.Value), ErrorCodes.QuestionMathToleranceInvalid);
            QuestionSchemaReader.AddIf(errors, (spec.Form ?? MathAnswerForm.Equivalent) != MathAnswerForm.Equivalent, ErrorCodes.QuestionMathToleranceFormConflict);
        }

        return errors;
    }

    public static (string Body, string GradingSpec) Normalize(JsonElement body, JsonElement gradingSpec)
    {
        var spec = QuestionSchemaReader.Read<MathStepsGradingSpec>(gradingSpec);
        var normalizedSpec = new MathStepsGradingSpec(QuestionSchemaReader.TrimAnswers(spec.AcceptedAnswers), spec.Form ?? MathAnswerForm.Equivalent, spec.Tolerance, spec.Tolerance is null ? null : spec.ToleranceMode);
        return (QuestionSchemaReader.Serialize(new MathStepsBody()), QuestionSchemaReader.Serialize(normalizedSpec));
    }
}
