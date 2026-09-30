using Elmanhg.Application.Shared.AiService;
using Elmanhg.Application.Shared.RichText;
using Elmanhg.Domain.Questions.Schemas;
using System.Text.Json;

namespace Elmanhg.Application.EssayGrading.Shared;

public static class EssayGradingRequestFactory
{
    public static AiEssayGradingRequest Create(string stemHtml, string gradingSpecJson, string essayText, EssayGradingContext? context, IRichTextExtractor extractor, int fieldMaxLength)
    {
        var spec = JsonSerializer.Deserialize<EssayGradingSpec>(gradingSpecJson, QuestionJson.SerializerOptions) ?? throw new InvalidOperationException("Essay grading spec is not readable.");
        var criteria = (spec.Criteria ?? [])
            .Select(x => Criterion(x, fieldMaxLength))
            .ToList();
        var modelAnswers = (spec.ModelAnswers ?? [])
            .Select(x => Plain(x, extractor, fieldMaxLength))
            .Where(x => x.Length > 0)
            .ToList();
        var objectives = (context?.Objectives ?? [])
            .Select(x => Truncate(x, fieldMaxLength))
            .ToList();
        return new AiEssayGradingRequest(Plain(stemHtml, extractor, fieldMaxLength), criteria, modelAnswers, essayText.Trim(), context?.SubjectName, objectives);
    }

    private static AiRubricCriterion Criterion(RubricCriterion criterion, int fieldMaxLength)
    {
        var levels = (criterion.Levels ?? [])
            .Select(x => new AiRubricLevel(x.Points.GetValueOrDefault(), Truncate(x.Description ?? string.Empty, fieldMaxLength)))
            .ToList();
        var description = criterion.Description is null ? null : Truncate(criterion.Description, fieldMaxLength);
        return new AiRubricCriterion(criterion.Id ?? string.Empty, Truncate(criterion.Title ?? string.Empty, fieldMaxLength), description, criterion.Points.GetValueOrDefault(), levels);
    }

    private static string Plain(string? html, IRichTextExtractor extractor, int fieldMaxLength) => Truncate(string.Join("\n", extractor.ExtractBlocks(html).Select(x => x.Text)), fieldMaxLength);

    private static string Truncate(string text, int maxLength) => text.Length <= maxLength ? text : text[..maxLength];
}
