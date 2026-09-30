using System.Text.Json.Serialization;

namespace Elmanhg.Domain.Questions.Grading;

public sealed record GradeFeedback(GradeFeedbackKind Kind, int Right, int Wrong, int Total)
{
    [JsonIgnore]
    public bool AwaitsReview => Kind == GradeFeedbackKind.MathUnchecked;

    public static GradeFeedback Unanswered { get; } = new(GradeFeedbackKind.Unanswered, 0, 0, 0);

    public static GradeFeedback NotANumber { get; } = new(GradeFeedbackKind.NotANumber, 0, 0, 0);

    public static GradeFeedback MathFinalAnswerOnly { get; } = new(GradeFeedbackKind.MathFinalAnswerOnly, 0, 0, 0);

    public static GradeFeedback MathWrongForm { get; } = new(GradeFeedbackKind.MathWrongForm, 0, 0, 0);

    public static GradeFeedback MathUnreadable { get; } = new(GradeFeedbackKind.MathUnreadable, 0, 0, 0);

    public static GradeFeedback MathUnchecked { get; } = new(GradeFeedbackKind.MathUnchecked, 0, 0, 0);

    public static GradeFeedback ChoiceTally(int right, int wrong, int total)
    {
        return new(GradeFeedbackKind.ChoiceTally, right, wrong, total);
    }

    public static GradeFeedback BlankTally(int right, int total)
    {
        return new(GradeFeedbackKind.BlankTally, right, 0, total);
    }

    public static GradeFeedback PlacementTally(int right, int wrong, int total)
    {
        return new(GradeFeedbackKind.PlacementTally, right, wrong, total);
    }
}
