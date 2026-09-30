using Elmanhg.Application.Mastery.Shared;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.MathStepGrading;
using Elmanhg.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Application.MathStepGrading.Shared;

public static class MathStepAttemptRecorder
{
    public static async Task RecordAsync(MathStepGrade grade, ISessionRepository sessionRepository, IQuestionMasteryRepository questionMasteryRepository, decimal correctThreshold, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var session = await sessionRepository.FirstOrDefaultAsync(x => x.Id == grade.SessionId && x.StudentId == grade.StudentId, cancellationToken, include: query => query.Include(x => x.Items).Include(x => x.Attempts).AsSplitQuery()).ConfigureAwait(false);
        var item = session?.GetItem(grade.QuestionId);
        if (session is null || item is null)
        {
            return;
        }

        var attempt = session.RecordAiGradedAttempt(item, grade.Answer, grade.ToQuestionGrade(), grade.GradedBy, grade.TimeTakenMilliseconds, grade.RequestedAt, now);
        if (attempt is null || session.IsTestMode)
        {
            return;
        }

        await QuestionMasteryRecorder.RecordAsync(attempt, questionMasteryRepository, correctThreshold, cancellationToken).ConfigureAwait(false);
    }
}
