using Elmanhg.Application.ContentRetrieval.Shared;

namespace Elmanhg.Application.Avatar.Shared;

public static class AvatarCitationMapper
{
    public static List<AvatarCitationResult> Map(IReadOnlyList<string> citations, IReadOnlyList<LessonContentMatchResult> matches, Guid? lessonId)
    {
        if (lessonId is not { } id)
        {
            return [];
        }

        return citations
            .Distinct()
            .Select(reference => matches.FirstOrDefault(x => x.Reference == reference))
            .OfType<LessonContentMatchResult>()
            .Select(match => new AvatarCitationResult(match.Reference, match.Section, match.SectionTitle, id, match.QuestionId))
            .ToList();
    }
}
