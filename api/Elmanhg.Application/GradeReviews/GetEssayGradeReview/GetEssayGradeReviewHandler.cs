using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.GradeReviews.Shared;
using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Units;
using MediatR;

namespace Elmanhg.Application.GradeReviews.GetEssayGradeReview;

public sealed class GetEssayGradeReviewHandler(IEssayGradeRepository essayGradeRepository, ISessionRepository sessionRepository, IQuestionRepository questionRepository, ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository) : IRequestHandler<GetEssayGradeReviewQuery, GradeReviewDetailResult>
{
    public async Task<GradeReviewDetailResult> Handle(GetEssayGradeReviewQuery request, CancellationToken cancellationToken)
    {
        var grade = await essayGradeRepository.FirstOrDefaultAsync(x => x.Id == request.EssayGradeId && x.SubjectId == request.SubjectId && x.ReviewReason != null, cancellationToken, asNoTracking: true).ConfigureAwait(false) ?? throw new NotFoundCoreException(ErrorCodes.GradeReviewNotFound);
        await GradeReviewSessionGuard.EnsureNotTestModeAsync(grade.SessionId, sessionRepository, cancellationToken).ConfigureAwait(false);
        var snapshot = await GradeReviewRevisionLoader.LoadAsync(grade.QuestionId, grade.QuestionVersion, questionRepository, cancellationToken).ConfigureAwait(false);
        var (_, placements) = await GradeReviewPlacementLoader.LoadAsync([grade.QuestionId], questionRepository, lessonRepository, unitRepository, cancellationToken).ConfigureAwait(false);
        return GradeReviewDetailGenerator.Essay(grade, snapshot, placements.GetValueOrDefault(grade.QuestionId) ?? GradeReviewPlacement.Unknown);
    }
}
