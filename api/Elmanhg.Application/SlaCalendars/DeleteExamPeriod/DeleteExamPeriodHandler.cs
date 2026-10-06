using Core.DDD.Repositories;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.SlaCalendars;
using MediatR;

namespace Elmanhg.Application.SlaCalendars.DeleteExamPeriod;

public sealed class DeleteExamPeriodHandler(IExamPeriodRepository examPeriodRepository, ICurrentUserService currentUserService) : IRequestHandler<DeleteExamPeriodCommand>
{
    public async Task Handle(DeleteExamPeriodCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);

        var examPeriod = await examPeriodRepository.GetRequiredAsync(request.ExamPeriodId, ErrorCodes.ExamPeriodNotFound, cancellationToken).ConfigureAwait(false);

        examPeriod.Delete(userId);

        await examPeriodRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
