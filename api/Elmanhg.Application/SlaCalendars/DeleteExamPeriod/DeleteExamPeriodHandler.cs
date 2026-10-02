using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.SlaCalendars;
using MediatR;

namespace Elmanhg.Application.SlaCalendars.DeleteExamPeriod;

public sealed class DeleteExamPeriodHandler(IExamPeriodRepository examPeriodRepository, ICurrentUserService currentUserService) : IRequestHandler<DeleteExamPeriodCommand>
{
    public async Task Handle(DeleteExamPeriodCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var examPeriod = await examPeriodRepository.GetByIdAsync(request.ExamPeriodId, cancellationToken).ConfigureAwait(false) ?? throw new NotFoundCoreException(ErrorCodes.ExamPeriodNotFound);

        examPeriod.Delete(currentUserService.UserId.Value);

        await examPeriodRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
