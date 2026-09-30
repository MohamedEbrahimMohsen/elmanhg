using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.EssayGrading;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.EssayGrading.FailEssayGrade;

public sealed class FailEssayGradeHandler(IEssayGradeRepository essayGradeRepository, IOptions<EssayGradingOptions> essayGradingOptions, TimeProvider timeProvider) : IRequestHandler<FailEssayGradeCommand>
{
    public async Task Handle(FailEssayGradeCommand request, CancellationToken cancellationToken)
    {
        var grade = await essayGradeRepository.FirstOrDefaultAsync(x => x.Id == request.EssayGradeId, cancellationToken).ConfigureAwait(false);
        if (grade is null || grade.Status != EssayGradeStatus.Pending)
        {
            return;
        }

        var options = essayGradingOptions.Value;
        grade.FailAttempt(request.ErrorCode, timeProvider.GetUtcNow(), options.MaxAttempts, TimeSpan.FromSeconds(options.RetryBaseDelaySeconds));

        await essayGradeRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
