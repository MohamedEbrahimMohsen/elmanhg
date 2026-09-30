using Elmanhg.Application.EssayGrading.Shared;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Application.Shared.RichText;
using Elmanhg.Domain.Questions.Schemas;

namespace Elmanhg.Application.MathStepGrading.Shared;

public static class MathStepGradingRequestFactory
{
    public static AiMathStepGradingRequest Create(string stemHtml, MathStepsGradingSpec spec, MathStepsAnswer answer, EssayGradingContext? context, IRichTextExtractor extractor, int fieldMaxLength)
    {
        var modelSolution = (spec.ModelSolution ?? [])
            .Select(x => x.Trim())
            .ToList();
        var acceptedAnswers = (spec.AcceptedAnswers ?? [])
            .Select(x => x.Trim())
            .ToList();
        var steps = (answer.Steps ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .ToList();
        var objectives = (context?.Objectives ?? [])
            .Select(x => Truncate(x, fieldMaxLength))
            .ToList();
        var question = Truncate(string.Join("\n", extractor.ExtractBlocks(stemHtml).Select(x => x.Text)), fieldMaxLength);
        return new AiMathStepGradingRequest(question, modelSolution, acceptedAnswers, steps, (answer.FinalAnswer ?? string.Empty).Trim(), context?.SubjectName, objectives);
    }

    private static string Truncate(string text, int maxLength) => text.Length <= maxLength ? text : text[..maxLength];
}
