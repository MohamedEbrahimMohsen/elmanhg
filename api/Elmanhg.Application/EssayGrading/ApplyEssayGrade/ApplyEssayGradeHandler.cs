using Elmanhg.Application.EssayGrading.Shared;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Sessions;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.EssayGrading.ApplyEssayGrade;

public sealed class ApplyEssayGradeHandler(IEssayGradeRepository essayGradeRepository, ISessionRepository sessionRepository, IQuestionMasteryRepository questionMasteryRepository, IOptions<MasteryOptions> masteryOptions, TimeProvider timeProvider) : IRequestHandler<ApplyEssayGradeCommand>
{
    public async Task Handle(ApplyEssayGradeCommand request, CancellationToken cancellationToken)
    {
        var grade = await essayGradeRepository.FirstOrDefaultAsync(x => x.Id == request.EssayGradeId, cancellationToken).ConfigureAwait(false);
        if (grade is null || !grade.IsAwaitingApplication)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        await EssayAttemptRecorder.RecordAsync(grade, sessionRepository, questionMasteryRepository, masteryOptions.Value.CorrectThreshold, now, cancellationToken).ConfigureAwait(false);
        grade.MarkApplied(now);

        await essayGradeRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
