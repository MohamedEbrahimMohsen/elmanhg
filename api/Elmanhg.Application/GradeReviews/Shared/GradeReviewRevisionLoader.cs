using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Questions;

namespace Elmanhg.Application.GradeReviews.Shared;

public static class GradeReviewRevisionLoader
{
    public static async Task<QuestionRevisionSnapshot> LoadAsync(Guid questionId, int version, IQuestionRepository questionRepository, CancellationToken cancellationToken)
    {
        var revisions = await questionRepository.GetRevisionsAsync([questionId], cancellationToken).ConfigureAwait(false);
        return revisions.FirstOrDefault(x => x.Version == version)?.ReadSnapshot() ?? throw new NotFoundCoreException(ErrorCodes.QuestionNotFound);
    }
}
