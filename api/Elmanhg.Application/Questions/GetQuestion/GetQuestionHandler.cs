using Core.DDD.Repositories;
using Core.Storage;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Domain.Questions;
using MediatR;

namespace Elmanhg.Application.Questions.GetQuestion;

public sealed class GetQuestionHandler(IQuestionRepository questionRepository, IFileStorage fileStorage) : IRequestHandler<GetQuestionQuery, QuestionDetailResult>
{
    public async Task<QuestionDetailResult> Handle(GetQuestionQuery request, CancellationToken cancellationToken)
    {
        var question = await questionRepository.GetRequiredAsync(request.QuestionId, ErrorCodes.QuestionNotFound, cancellationToken, asNoTracking: true).ConfigureAwait(false);

        return QuestionResultGenerator.GenerateDetail(question, fileStorage);
    }
}
