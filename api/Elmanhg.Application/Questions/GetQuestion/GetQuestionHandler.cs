using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Application.Shared.Storage;
using Elmanhg.Domain.Questions;
using MediatR;

namespace Elmanhg.Application.Questions.GetQuestion;

public sealed class GetQuestionHandler(IQuestionRepository questionRepository, IFileStorage fileStorage) : IRequestHandler<GetQuestionQuery, QuestionDetailResult>
{
    public async Task<QuestionDetailResult> Handle(GetQuestionQuery request, CancellationToken cancellationToken)
    {
        var question = await questionRepository.GetByIdAsync(request.QuestionId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        if (question is null)
        {
            throw new NotFoundCoreException(ErrorCodes.QuestionNotFound);
        }

        return QuestionResultGenerator.GenerateDetail(question, fileStorage);
    }
}
