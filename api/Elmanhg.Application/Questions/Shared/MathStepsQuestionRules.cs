using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Questions.Grading;
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

        QuestionSchemaReader.AddIf(errors, spec.ModelSolution is not null && !IsValidModelSolution(spec.ModelSolution, options), ErrorCodes.QuestionMathModelSolutionInvalid);
        QuestionSchemaReader.AddIf(errors, spec.StepsWeight is < 0 or > MathStepsGrader.PercentScale, ErrorCodes.QuestionMathStepsWeightInvalid);
        QuestionSchemaReader.AddIf(errors, spec.StepsWeight > 0 && spec.ModelSolution is not { Count: > 0 }, ErrorCodes.QuestionMathModelSolutionRequired);
        return errors;
    }

    public static (string Body, string GradingSpec) Normalize(JsonElement body, JsonElement gradingSpec)
    {
        var spec = QuestionSchemaReader.Read<MathStepsGradingSpec>(gradingSpec);
        var normalizedSpec = new MathStepsGradingSpec(QuestionSchemaReader.TrimAnswers(spec.AcceptedAnswers), spec.Form ?? MathAnswerForm.Equivalent, spec.Tolerance, spec.Tolerance is null ? null : spec.ToleranceMode, spec.ModelSolution is { Count: > 0 } solution ? QuestionSchemaReader.TrimAnswers(solution) : null, spec.StepsWeight is > 0 ? spec.StepsWeight : null);
        return (QuestionSchemaReader.Serialize(new MathStepsBody()), QuestionSchemaReader.Serialize(normalizedSpec));
    }

    private static bool IsValidModelSolution(List<string> steps, ContentOptions options) => steps.Count <= options.QuestionModelSolutionStepsMaxCount
        && steps.All(x => !string.IsNullOrWhiteSpace(x) && x.Trim().Length <= options.QuestionModelSolutionStepMaxLength);
}
