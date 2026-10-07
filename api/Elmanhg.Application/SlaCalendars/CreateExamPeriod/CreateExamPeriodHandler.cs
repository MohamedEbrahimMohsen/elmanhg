using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.SlaCalendars.Shared;
using Elmanhg.Domain.SlaCalendars;
using MediatR;

namespace Elmanhg.Application.SlaCalendars.CreateExamPeriod;

public sealed class CreateExamPeriodHandler(IExamPeriodRepository examPeriodRepository, ICurrentUserService currentUserService) : IRequestHandler<CreateExamPeriodCommand, ExamPeriodResult>
{
    public async Task<ExamPeriodResult> Handle(CreateExamPeriodCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);

        var examPeriod = ExamPeriod.Create(request.Name!.Trim(), request.StartDate!.Value, request.EndDate!.Value, userId);

        await examPeriodRepository.AddAsync(examPeriod, cancellationToken).ConfigureAwait(false);
        await examPeriodRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ExamPeriodResultGenerator.Generate(examPeriod);
    }
}
