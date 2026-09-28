using Core.Localization;
using Elmanhg.Domain.Questions.Grading;

namespace Elmanhg.Application.Questions.Shared.Grading;

public static class GradeFeedbackText
{
    public static string? Localize(GradeFeedback? feedback, ILocalizer localizer)
    {
        if (feedback is null)
        {
            return null;
        }

        return feedback.Kind switch
        {
            GradeFeedbackKind.Unanswered => localizer.GetMessage(GradeFeedbackKeys.Unanswered),
            GradeFeedbackKind.ChoiceTally => localizer.GetMessage(GradeFeedbackKeys.ChoiceTally, context: new Dictionary<string, object>
            {
                [GradeFeedbackKeys.RightArgument] = feedback.Right,
                [GradeFeedbackKeys.WrongArgument] = feedback.Wrong,
                [GradeFeedbackKeys.TotalArgument] = feedback.Total,
            }),
            GradeFeedbackKind.BlankTally => localizer.GetMessage(GradeFeedbackKeys.BlankTally, context: new Dictionary<string, object>
            {
                [GradeFeedbackKeys.RightArgument] = feedback.Right,
                [GradeFeedbackKeys.TotalArgument] = feedback.Total,
            }),
            GradeFeedbackKind.NotANumber => localizer.GetMessage(GradeFeedbackKeys.NotANumber),
            _ => throw new InvalidOperationException("Unsupported grade feedback."),
        };
    }
}
