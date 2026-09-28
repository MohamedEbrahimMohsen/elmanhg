using Core.Errors;
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
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var userId = currentUserService.UserId.Value;
        var session = await sessionRepository.FirstOrDefaultAsync(x => x.Id == request.SessionId && x.StudentId == userId && x.Kind != SessionKind.Quiz, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        if (session is null)
        {
            throw new NotFoundCoreException(ErrorCodes.SessionNotFound);
        }

        var attempts = await sessionRepository.GetExamAttemptsAsync(userId, session.Kind, session.ScopeKey, cancellationToken).ConfigureAwait(false);
        return ExamAttemptsResultGenerator.Generate(attempts);
    }
}
