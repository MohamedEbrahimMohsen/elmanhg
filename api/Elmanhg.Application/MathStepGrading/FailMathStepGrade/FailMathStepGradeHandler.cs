using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.Retries;
using Elmanhg.Domain.MathStepGrading;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.MathStepGrading.FailMathStepGrade;

public sealed class FailMathStepGradeHandler(IMathStepGradeRepository mathStepGradeRepository, IOptions<MathStepGradingOptions> mathStepGradingOptions, TimeProvider timeProvider) : FailRetriedWorkHandler<FailMathStepGradeCommand, MathStepGrade>(mathStepGradeRepository, timeProvider)
{
    protected override int MaxAttempts => mathStepGradingOptions.Value.MaxAttempts;
    protected override TimeSpan RetryBaseDelay => TimeSpan.FromSeconds(mathStepGradingOptions.Value.RetryBaseDelaySeconds);
}
