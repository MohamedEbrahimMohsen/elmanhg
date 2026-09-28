namespace Elmanhg.Domain.Questions.Grading;

public sealed record GradeFeedback(GradeFeedbackKind Kind, int Right, int Wrong, int Total)
{
    public static GradeFeedback Unanswered { get; } = new(GradeFeedbackKind.Unanswered, 0, 0, 0);

    public static GradeFeedback ChoiceTally(int right, int wrong, int total)
    {
        return new(GradeFeedbackKind.ChoiceTally, right, wrong, total);
    }
}
