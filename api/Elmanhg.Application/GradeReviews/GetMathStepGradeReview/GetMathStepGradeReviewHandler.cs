using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.GradeReviews.Shared;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.MathStepGrading;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Units;
using MediatR;

namespace Elmanhg.Application.GradeReviews.GetMathStepGradeReview;

public sealed class GetMathStepGradeReviewHandler(IMathStepGradeRepository mathStepGradeRepository, IQuestionRepository questionRepository, ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository) : IRequestHandler<GetMathStepGradeReviewQuery, GradeReviewDetailResult>
{
    public async Task<GradeReviewDetailResult> Handle(GetMathStepGradeReviewQuery request, CancellationToken cancellationToken)
    {
        var grade = await mathStepGradeRepository.FirstOrDefaultAsync(x => x.Id == request.MathStepGradeId && x.SubjectId == request.SubjectId && x.ReviewReason != null, cancellationToken, asNoTracking: true).ConfigureAwait(false) ?? throw new NotFoundCoreException(ErrorCodes.GradeReviewNotFound);
        var snapshot = await GradeReviewRevisionLoader.LoadAsync(grade.QuestionId, grade.QuestionVersion, questionRepository, cancellationToken).ConfigureAwait(false);
        var (_, placements) = await GradeReviewPlacementLoader.LoadAsync([grade.QuestionId], questionRepository, lessonRepository, unitRepository, cancellationToken).ConfigureAwait(false);
        return GradeReviewDetailGenerator.MathSteps(grade, snapshot, placements.GetValueOrDefault(grade.QuestionId) ?? GradeReviewPlacement.Unknown);
    }
}
