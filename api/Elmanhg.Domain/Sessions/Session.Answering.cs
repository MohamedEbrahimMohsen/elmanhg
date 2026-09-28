using Core.Errors;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Questions.Schemas;
using Elmanhg.Domain.SharedKernel.Exceptions;

namespace Elmanhg.Domain.Sessions;

public partial class Session
{
    public SessionItem? GetItem(Guid questionId) => Items.FirstOrDefault(x => x.QuestionId == questionId);

    public Attempt? FindAttempt(Guid questionId) => Attempts.FirstOrDefault(x => x.QuestionId == questionId);

    public Attempt RecordAttempt(SessionItem item, string answer, QuestionGrade grade, int? reportedTimeTakenMilliseconds)
    {
        if (IsExam)
        {
            throw new InvalidOperationException("Exam answers are saved with SaveExamAnswer.");
        }

        if (!Items.Contains(item))
        {
            throw new InvalidOperationException("Session item does not belong to this session.");
        }

        var existing = FindAttempt(item.QuestionId);
        if (existing is not null)
        {
            if (QuestionJson.AreEquivalent(existing.Answer, answer))
            {
                return existing;
            }

            throw new ConflictCoreException(ErrorCodes.SessionQuestionAlreadyAnswered);
        }

        EnsureNotSubmitted();
        var now = UtcNowToMicroseconds();
        var attempt = Attempt.Create(this, item, answer, grade, MeasureTimeTaken(LastActivityAt, now, reportedTimeTakenMilliseconds), now);
        Attempts.Add(attempt);
        Touch(now);
        return attempt;
    }

    private static int MeasureTimeTaken(DateTimeOffset since, DateTimeOffset now, int? reportedMilliseconds)
    {
        var elapsed = (int)Math.Clamp((now - since).TotalMilliseconds, 0, int.MaxValue);
        return reportedMilliseconds is null ? elapsed : Math.Clamp(reportedMilliseconds.Value, 0, elapsed);
    }
}
