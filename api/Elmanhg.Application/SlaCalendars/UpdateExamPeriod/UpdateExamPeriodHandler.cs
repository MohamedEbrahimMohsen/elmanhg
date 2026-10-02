using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.SlaCalendars.Shared;
using Elmanhg.Domain.SlaCalendars;
using MediatR;

namespace Elmanhg.Application.SlaCalendars.UpdateExamPeriod;

public sealed class UpdateExamPeriodHandler(IExamPeriodRepository examPeriodRepository, ICurrentUserService currentUserService) : IRequestHandler<UpdateExamPeriodCommand, ExamPeriodResult>
{
    public async Task<ExamPeriodResult> Handle(UpdateExamPeriodCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var examPeriod = await examPeriodRepository.GetByIdAsync(request.ExamPeriodId, cancellationToken).ConfigureAwait(false) ?? throw new NotFoundCoreException(ErrorCodes.ExamPeriodNotFound);

        examPeriod.Update(request.Name!.Trim(), request.StartDate!.Value, request.EndDate!.Value, currentUserService.UserId.Value);

        await examPeriodRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ExamPeriodResultGenerator.Generate(examPeriod);
    }
}
