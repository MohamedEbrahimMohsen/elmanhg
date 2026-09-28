using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exams.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Units;
using MediatR;

namespace Elmanhg.Application.Exams.GetUnitExamAttempts;

public sealed class GetUnitExamAttemptsHandler(ICurriculumUnitRepository unitRepository, ISessionRepository sessionRepository, ICurrentUserService currentUserService) : IRequestHandler<GetUnitExamAttemptsQuery, ExamAttemptsResult>
{
    public async Task<ExamAttemptsResult> Handle(GetUnitExamAttemptsQuery request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var userId = currentUserService.UserId.Value;
        var unit = await unitRepository.GetByIdAsync(request.UnitId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        if (unit is null)
        {
            throw new NotFoundCoreException(ErrorCodes.UnitNotFound);
        }

        var attempts = await sessionRepository.GetExamAttemptsAsync(userId, SessionKind.UnitExam, new UnitExamScope(unit.Id).ToKey(), cancellationToken).ConfigureAwait(false);
        return ExamAttemptsResultGenerator.Generate(attempts);
    }
}
