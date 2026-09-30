using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Sessions;

namespace Elmanhg.Application.Mastery.Shared;

public static class QuestionMasteryRecorder
{
    public static async Task RecordAsync(Attempt attempt, IQuestionMasteryRepository questionMasteryRepository, decimal correctThreshold, CancellationToken cancellationToken)
    {
        var masteryAttempt = MasteryAttempt.From(attempt);
        var mastery = await questionMasteryRepository.FirstOrDefaultAsync(x => x.StudentId == attempt.StudentId && x.QuestionId == attempt.QuestionId, cancellationToken).ConfigureAwait(false);
        if (mastery is null)
        {
            await questionMasteryRepository.AddAsync(QuestionMastery.Start(attempt.StudentId, attempt.QuestionId, masteryAttempt), cancellationToken).ConfigureAwait(false);
            return;
        }

        mastery.Record(masteryAttempt, correctThreshold);
    }
}
