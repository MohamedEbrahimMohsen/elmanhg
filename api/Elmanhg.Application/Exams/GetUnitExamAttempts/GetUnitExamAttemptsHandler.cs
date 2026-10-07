using Core.DDD.Repositories;
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
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);
        var unit = await unitRepository.GetRequiredAsync(request.UnitId, ErrorCodes.UnitNotFound, cancellationToken, asNoTracking: true).ConfigureAwait(false);

        var attempts = await sessionRepository.GetExamAttemptsAsync(userId, SessionKind.UnitExam, new UnitExamScope(unit.Id).ToKey(), cancellationToken).ConfigureAwait(false);
        return ExamAttemptsResultGenerator.Generate(attempts);
    }
}
