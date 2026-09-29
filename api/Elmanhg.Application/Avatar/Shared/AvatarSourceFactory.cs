using Elmanhg.Application.ContentRetrieval.Shared;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Domain.ContentRetrieval;

namespace Elmanhg.Application.Avatar.Shared;

public static class AvatarSourceFactory
{
    // Section labels are text shown to the model, in the lesson's language.
    private const string ExplanationLabel = "الشرح";
    private const string ObjectivesLabel = "الأهداف";
    private const string SummaryLabel = "الملخص";
    private const string QuestionExplanationLabel = "شرح سؤال";

    public static AiChatSource Create(LessonContentMatchResult match) => new(match.Reference, Title(match), match.Content);

    private static string Title(LessonContentMatchResult match)
    {
        var label = match.Section switch
        {
            LessonContentSection.Explanation => ExplanationLabel,
            LessonContentSection.Objectives => ObjectivesLabel,
            LessonContentSection.Summary => SummaryLabel,
            LessonContentSection.QuestionExplanation => QuestionExplanationLabel,
            _ => ExplanationLabel,
        };
        return string.IsNullOrWhiteSpace(match.SectionTitle) ? label : label + " — " + match.SectionTitle;
    }
}
