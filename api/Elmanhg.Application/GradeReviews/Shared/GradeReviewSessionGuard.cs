using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Sessions;

namespace Elmanhg.Application.GradeReviews.Shared;

public static class GradeReviewSessionGuard
{
    public static async Task EnsureNotTestModeAsync(Guid sessionId, ISessionRepository sessionRepository, CancellationToken cancellationToken)
    {
        var testModeSessions = await sessionRepository.CountAsync(cancellationToken, x => x.Id == sessionId && x.IsTestMode).ConfigureAwait(false);
        if (testModeSessions > 0)
        {
            throw new NotFoundCoreException(ErrorCodes.GradeReviewNotFound);
        }
    }
}
