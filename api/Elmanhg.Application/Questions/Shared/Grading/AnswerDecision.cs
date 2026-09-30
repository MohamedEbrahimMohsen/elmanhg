using Elmanhg.Domain.Questions.Grading;

namespace Elmanhg.Application.Questions.Shared.Grading;

public sealed record AnswerDecision(QuestionGrade? Grade, MathAnswerVerdict? Verdict)
{
    public static AnswerDecision Graded(QuestionGrade grade) => new(grade, null);

    public static AnswerDecision Deferred(MathAnswerVerdict? verdict) => new(null, verdict);
}
