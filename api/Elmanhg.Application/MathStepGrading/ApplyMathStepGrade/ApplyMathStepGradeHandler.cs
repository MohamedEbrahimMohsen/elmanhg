using Elmanhg.Application.MathStepGrading.Shared;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.MathStepGrading;
using Elmanhg.Domain.Sessions;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.MathStepGrading.ApplyMathStepGrade;

public sealed class ApplyMathStepGradeHandler(IMathStepGradeRepository mathStepGradeRepository, ISessionRepository sessionRepository, IQuestionMasteryRepository questionMasteryRepository, IOptions<MasteryOptions> masteryOptions, TimeProvider timeProvider) : IRequestHandler<ApplyMathStepGradeCommand>
{
    public async Task Handle(ApplyMathStepGradeCommand request, CancellationToken cancellationToken)
    {
        var grade = await mathStepGradeRepository.FirstOrDefaultAsync(x => x.Id == request.MathStepGradeId, cancellationToken).ConfigureAwait(false);
        if (grade is null || !grade.IsAwaitingApplication)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        await MathStepAttemptRecorder.RecordAsync(grade, sessionRepository, questionMasteryRepository, masteryOptions.Value.CorrectThreshold, now, cancellationToken).ConfigureAwait(false);
        grade.MarkApplied(now);

        await mathStepGradeRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
