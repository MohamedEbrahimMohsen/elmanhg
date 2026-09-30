using Elmanhg.Domain.Questions.Grading;

namespace Elmanhg.Domain.Sessions;

public partial class Session
{
    // Exam questions share one page; per-question time is not observable, the exam's time is SubmittedAt - StartedAt.
    private const int ExamAttemptTimeTakenMilliseconds = 0;

    public List<Attempt> SubmitExam(IReadOnlyDictionary<Guid, QuestionGrade> grades, DateTimeOffset now) => SubmitExam(grades, new HashSet<Guid>(), now);

    public List<Attempt> SubmitExam(IReadOnlyDictionary<Guid, QuestionGrade> grades, IReadOnlySet<Guid> deferredQuestionIds, DateTimeOffset now)
    {
        if (!IsExam)
        {
            throw new InvalidOperationException("Quizzes are finished with Submit.");
        }

        if (IsSubmitted)
        {
            return [];
        }

        var answered = Items
            .Where(x => x.SavedAnswer is not null && !deferredQuestionIds.Contains(x.QuestionId))
            .OrderBy(x => x.Position)
            .ToList();
        if (answered.Any(x => !grades.ContainsKey(x.QuestionId)))
        {
            throw new InvalidOperationException("A saved exam answer has no grade.");
        }

        var at = ToMicroseconds(now);
        var attempts = answered
            .Select(x => Attempt.Create(this, x, x.SavedAnswer ?? string.Empty, grades[x.QuestionId], ExamAttemptTimeTakenMilliseconds, at, AttemptGrader.Auto))
            .ToList();
        Attempts.AddRange(attempts);
        ScorePercent = CalculateScorePercent();
        SubmittedAt = at;
        Touch(at);
        if (attempts.Count > 0)
        {
            RaiseDomainEvent(new AttemptsRecorded(this, attempts));
        }

        return attempts;
    }
}
