using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Questions;
using MediatR;

namespace Elmanhg.Application.Questions.RetireQuestion;

public sealed class RetireQuestionHandler(IQuestionRepository questionRepository, ICurrentUserService currentUserService) : IRequestHandler<RetireQuestionCommand>
{
    public async Task Handle(RetireQuestionCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var question = await questionRepository.GetByIdAsync(request.QuestionId, cancellationToken).ConfigureAwait(false);
        if (question is null)
        {
            throw new NotFoundCoreException(ErrorCodes.QuestionNotFound);
        }

        question.Retire(currentUserService.UserId.Value);

        await questionRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
