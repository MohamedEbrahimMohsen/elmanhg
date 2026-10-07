using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.Retries;
using Elmanhg.Domain.EssayGrading;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.EssayGrading.FailEssayGrade;

public sealed class FailEssayGradeHandler(IEssayGradeRepository essayGradeRepository, IOptions<EssayGradingOptions> essayGradingOptions, TimeProvider timeProvider) : FailRetriedWorkHandler<FailEssayGradeCommand, EssayGrade>(essayGradeRepository, timeProvider)
{
    protected override int MaxAttempts => essayGradingOptions.Value.MaxAttempts;
    protected override TimeSpan RetryBaseDelay => TimeSpan.FromSeconds(essayGradingOptions.Value.RetryBaseDelaySeconds);
}
