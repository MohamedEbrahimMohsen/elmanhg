using Core.Errors;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Questions.Schemas;
using Elmanhg.Domain.SharedKernel.Exceptions;

namespace Elmanhg.Domain.Sessions;

public partial class Session
{
    public string? FindPendingEssayAnswer(SessionItem item) => !IsExam && item.SavedAnswer is not null && FindAttempt(item.QuestionId) is null ? item.SavedAnswer : null;

    public EssaySubmission SubmitEssay(SessionItem item, string answer, int? reportedTimeTakenMilliseconds)
    {
        if (IsExam)
        {
            throw new InvalidOperationException("Exam essays are saved with SaveExamAnswer.");
        }

        EnsureOwnItem(item);
        var submitted = FindAttempt(item.QuestionId)?.Answer ?? item.SavedAnswer;
        if (submitted is not null)
        {
            if (QuestionJson.AreEquivalent(submitted, answer))
            {
                return EssaySubmission.Replay;
            }

            throw new ConflictCoreException(ErrorCodes.SessionQuestionAlreadyAnswered);
        }

        EnsureNotSubmitted();
        var now = UtcNowToMicroseconds();
        var timeTaken = MeasureTimeTaken(LastActivityAt, now, reportedTimeTakenMilliseconds);
        item.SaveAnswer(answer, now);
        Touch(now);
        return new EssaySubmission(true, timeTaken, now);
    }

    public Attempt? RecordEssayAttempt(SessionItem item, string answer, QuestionGrade grade, AttemptGrader gradedBy, int timeTakenMilliseconds, DateTimeOffset answeredAt, DateTimeOffset now)
    {
        EnsureOwnItem(item);
        if (FindAttempt(item.QuestionId) is not null)
        {
            return null;
        }

        var attempt = Attempt.Create(this, item, answer, grade, timeTakenMilliseconds, ToMicroseconds(answeredAt), gradedBy);
        Attempts.Add(attempt);
        if (IsSubmitted)
        {
            ScorePercent = CalculateScorePercent();
        }

        // Stamping the row makes the xmin token serialise this against a concurrent answer or finish.
        UpdationDate = ToMicroseconds(now);
        RaiseDomainEvent(new AttemptsRecorded(this, [attempt]));
        return attempt;
    }

    private void EnsureOwnItem(SessionItem item)
    {
        if (!Items.Contains(item))
        {
            throw new InvalidOperationException("Session item does not belong to this session.");
        }
    }
}
