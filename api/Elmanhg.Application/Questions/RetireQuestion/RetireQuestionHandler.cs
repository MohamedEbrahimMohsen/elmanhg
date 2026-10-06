using Core.DDD.Repositories;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Questions;
using MediatR;

namespace Elmanhg.Application.Questions.RetireQuestion;

public sealed class RetireQuestionHandler(IQuestionRepository questionRepository, ICurrentUserService currentUserService) : IRequestHandler<RetireQuestionCommand>
{
    public async Task Handle(RetireQuestionCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);

        var question = await questionRepository.GetRequiredAsync(request.QuestionId, ErrorCodes.QuestionNotFound, cancellationToken).ConfigureAwait(false);

        question.Retire(userId);

        await questionRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
