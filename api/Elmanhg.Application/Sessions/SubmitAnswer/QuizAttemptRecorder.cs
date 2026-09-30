using Elmanhg.Application.Mastery.Shared;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Sessions;

namespace Elmanhg.Application.Sessions.SubmitAnswer;

public static class QuizAttemptRecorder
{
    public static async Task RecordAsync(Session session, SessionItem item, QuestionGrade grade, string answer, int? reportedTimeTakenMilliseconds, IQuestionMasteryRepository questionMasteryRepository, decimal correctThreshold, CancellationToken cancellationToken)
    {
        var attempt = session.RecordAttempt(item, answer, grade, reportedTimeTakenMilliseconds);
        if (session.IsTestMode || grade.AwaitsReview)
        {
            return;
        }

        await QuestionMasteryRecorder.RecordAsync(attempt, questionMasteryRepository, correctThreshold, cancellationToken).ConfigureAwait(false);
    }
}
