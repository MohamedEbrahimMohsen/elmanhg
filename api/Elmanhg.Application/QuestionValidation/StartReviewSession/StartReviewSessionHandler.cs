using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.QuestionValidation.Shared;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.ReviewSessions;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.QuestionValidation.StartReviewSession;

public sealed class StartReviewSessionHandler(IReviewSessionRepository reviewSessionRepository, IOptions<QuestionValidationOptions> questionValidationOptions, ICurrentUserService currentUserService) : IRequestHandler<StartReviewSessionCommand, ReviewSessionResult>
{
    public async Task<ReviewSessionResult> Handle(StartReviewSessionCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var session = ReviewSession.Start(currentUserService.UserId.Value, TimeSpan.FromMinutes(questionValidationOptions.Value.ReviewSessionLifetimeMinutes));

        await reviewSessionRepository.AddAsync(session, cancellationToken).ConfigureAwait(false);
        await reviewSessionRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new ReviewSessionResult(session.Id, session.ExpiresAt);
    }
}
