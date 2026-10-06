using Core.DDD.Repositories;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.GradeReviews.Shared;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.MathStepGrading;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Units;
using MediatR;

namespace Elmanhg.Application.GradeReviews.GetMathStepGradeReview;

public sealed class GetMathStepGradeReviewHandler(IMathStepGradeRepository mathStepGradeRepository, ISessionRepository sessionRepository, IQuestionRepository questionRepository, ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository) : IRequestHandler<GetMathStepGradeReviewQuery, GradeReviewDetailResult>
{
    public async Task<GradeReviewDetailResult> Handle(GetMathStepGradeReviewQuery request, CancellationToken cancellationToken)
    {
        var grade = await mathStepGradeRepository.GetRequiredAsync(x => x.Id == request.MathStepGradeId && x.SubjectId == request.SubjectId && x.ReviewReason != null, ErrorCodes.GradeReviewNotFound, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        await GradeReviewSessionGuard.EnsureNotTestModeAsync(grade.SessionId, sessionRepository, cancellationToken).ConfigureAwait(false);
        var snapshot = await GradeReviewRevisionLoader.LoadAsync(grade.QuestionId, grade.QuestionVersion, questionRepository, cancellationToken).ConfigureAwait(false);
        var (_, placements) = await GradeReviewPlacementLoader.LoadAsync([grade.QuestionId], questionRepository, lessonRepository, unitRepository, cancellationToken).ConfigureAwait(false);
        return GradeReviewDetailGenerator.MathSteps(grade, snapshot, placements.GetValueOrDefault(grade.QuestionId) ?? GradeReviewPlacement.Unknown);
    }
}
