namespace Elmanhg.Domain.Questions.Grading;

public sealed record NormalisedGrade(decimal Value, GradeFeedback? Feedback)
{
    public static NormalisedGrade Unanswered { get; } = new(0m, GradeFeedback.Unanswered);
}
