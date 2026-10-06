using Core.DDD.Repositories;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exams.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Sessions;
using MediatR;

namespace Elmanhg.Application.Exams.GetExamAttempts;

public sealed class GetExamAttemptsHandler(ISessionRepository sessionRepository, ICurrentUserService currentUserService) : IRequestHandler<GetExamAttemptsQuery, ExamAttemptsResult>
{
    public async Task<ExamAttemptsResult> Handle(GetExamAttemptsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);
        var session = await sessionRepository.GetRequiredAsync(x => x.Id == request.SessionId && x.StudentId == userId && x.Kind != SessionKind.Quiz, ErrorCodes.SessionNotFound, cancellationToken, asNoTracking: true).ConfigureAwait(false);

        var attempts = await sessionRepository.GetExamAttemptsAsync(userId, session.Kind, session.ScopeKey, cancellationToken).ConfigureAwait(false);
        return ExamAttemptsResultGenerator.Generate(attempts);
    }
}
