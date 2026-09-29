using Elmanhg.Domain.ContentRetrieval;

namespace Elmanhg.Application.ContentRetrieval.Shared;

public static class LessonContentMatchResultGenerator
{
    public static LessonContentMatchResult Generate(LessonContentMatch match)
    {
        return new LessonContentMatchResult(match.ChunkId, match.Section, match.SectionTitle, match.Position, match.QuestionId, Reference(match), match.Content, 1 - match.Distance);
    }

    private static string Reference(LessonContentMatch match)
    {
        var position = match.Position;
        return match.Section switch
        {
            LessonContentSection.Explanation => $"explanation-{position}",
            LessonContentSection.Objectives => $"objectives-{position}",
            LessonContentSection.Summary => $"summary-{position}",
            LessonContentSection.QuestionExplanation => $"question-{match.QuestionId}-{position}",
            _ => $"section-{position}",
        };
    }
}
