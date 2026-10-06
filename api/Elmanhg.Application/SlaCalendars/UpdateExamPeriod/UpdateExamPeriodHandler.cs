using Core.DDD.Repositories;
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
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);

        var examPeriod = await examPeriodRepository.GetRequiredAsync(request.ExamPeriodId, ErrorCodes.ExamPeriodNotFound, cancellationToken).ConfigureAwait(false);

        examPeriod.Update(request.Name!.Trim(), request.StartDate!.Value, request.EndDate!.Value, userId);

        await examPeriodRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ExamPeriodResultGenerator.Generate(examPeriod);
    }
}
