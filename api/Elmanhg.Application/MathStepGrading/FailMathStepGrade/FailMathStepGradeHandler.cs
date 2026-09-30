using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.MathStepGrading;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.MathStepGrading.FailMathStepGrade;

public sealed class FailMathStepGradeHandler(IMathStepGradeRepository mathStepGradeRepository, IOptions<MathStepGradingOptions> mathStepGradingOptions, TimeProvider timeProvider) : IRequestHandler<FailMathStepGradeCommand>
{
    public async Task Handle(FailMathStepGradeCommand request, CancellationToken cancellationToken)
    {
        var grade = await mathStepGradeRepository.FirstOrDefaultAsync(x => x.Id == request.MathStepGradeId, cancellationToken).ConfigureAwait(false);
        if (grade is null || grade.Status != MathStepGradeStatus.Pending)
        {
            return;
        }

        var options = mathStepGradingOptions.Value;
        grade.FailAttempt(request.ErrorCode, timeProvider.GetUtcNow(), options.MaxAttempts, TimeSpan.FromSeconds(options.RetryBaseDelaySeconds));

        await mathStepGradeRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
