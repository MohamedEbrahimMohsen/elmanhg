using Core.Errors;
using Core.Utilities.Time;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Questions.Schemas;
using Elmanhg.Domain.SharedKernel.Exceptions;

namespace Elmanhg.Domain.Sessions;

public partial class Session
{
    public SessionItem? GetItem(Guid questionId) => Items.FirstOrDefault(x => x.QuestionId == questionId);

    public Attempt? FindAttempt(Guid questionId) => Attempts.FirstOrDefault(x => x.QuestionId == questionId);

    // True for an equivalent resubmission; throws for a different answer or a submitted session.
    public bool IsReplay(SessionItem item, string answer)
    {
        EnsureOwnItem(item);
        var submitted = FindAttempt(item.QuestionId)?.Answer ?? FindPendingEssayAnswer(item);
        if (submitted is null)
        {
            EnsureNotSubmitted();
            return false;
        }

        if (QuestionJson.AreEquivalent(submitted, answer))
        {
            return true;
        }

        throw new ConflictCoreException(ErrorCodes.SessionQuestionAlreadyAnswered);
    }

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

        if (FindPendingEssayAnswer(item) is not null)
        {
            throw new ConflictCoreException(ErrorCodes.SessionQuestionAlreadyAnswered);
        }

        EnsureNotSubmitted();
        var now = DateTimeOffset.UtcNow.TruncateToMicroseconds();
        var attempt = Attempt.Create(this, item, answer, grade, MeasureTimeTaken(LastActivityAt, now, reportedTimeTakenMilliseconds), now, AttemptGrader.Auto);
        Attempts.Add(attempt);
        Touch(now);
        RaiseDomainEvent(new AttemptsRecorded(this, [attempt]));
        return attempt;
    }

    private static int MeasureTimeTaken(DateTimeOffset since, DateTimeOffset now, int? reportedMilliseconds)
    {
        var elapsed = (int)Math.Clamp((now - since).TotalMilliseconds, 0, int.MaxValue);
        return reportedMilliseconds is null ? elapsed : Math.Clamp(reportedMilliseconds.Value, 0, elapsed);
    }
}
