using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Questions.Schemas;
using System.Text.Json;

namespace Elmanhg.Application.Questions.Shared;

public static class MathStepsAnswerRules
{
    public static bool CanRead(JsonElement answer) => QuestionSchemaReader.TryRead<MathStepsAnswer>(answer, out var math) && (math.Steps is null || math.Steps.All(x => x is not null));

    public static string Canonicalize(JsonElement answer)
    {
        var math = QuestionSchemaReader.Read<MathStepsAnswer>(answer);
        var steps = (math.Steps ?? [])
            .Select(x => x!.Trim())
            .Where(x => x.Length > 0)
            .ToList<string?>();
        return QuestionSchemaReader.Serialize(new MathStepsAnswer(steps, (math.FinalAnswer ?? string.Empty).Trim()));
    }

    public static bool HasFinalAnswer(JsonElement answer) => !string.IsNullOrWhiteSpace(QuestionSchemaReader.Read<MathStepsAnswer>(answer).FinalAnswer);

    public static bool ExceedsLimits(JsonElement answer, SessionsOptions options)
    {
        var math = QuestionSchemaReader.Read<MathStepsAnswer>(answer);
        var steps = math.Steps ?? [];
        return steps.Count > options.MathStepsMaxCount
            || steps.Any(x => x is not null && x.Length > options.MathStepMaxLength)
            || (math.FinalAnswer?.Length ?? 0) > options.MathFinalAnswerMaxLength;
    }
}
