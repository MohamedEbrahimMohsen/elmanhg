namespace Elmanhg.Domain.Questions.Grading;

public static class MathStepsGrader
{
    public static NormalisedGrade Grade(MathAnswerVerdict? verdict) => verdict switch
    {
        null => NormalisedGrade.Unanswered,
        MathAnswerVerdict.Equivalent => new(1m, GradeFeedback.MathFinalAnswerOnly),
        MathAnswerVerdict.NotEquivalent => new(0m, GradeFeedback.MathFinalAnswerOnly),
        MathAnswerVerdict.WrongForm => new(0m, GradeFeedback.MathWrongForm),
        MathAnswerVerdict.Unreadable => new(0m, GradeFeedback.MathUnreadable),
        MathAnswerVerdict.Unchecked => new(0m, GradeFeedback.MathUnchecked),
        _ => throw new InvalidOperationException("Unsupported math verdict."),
    };
}
