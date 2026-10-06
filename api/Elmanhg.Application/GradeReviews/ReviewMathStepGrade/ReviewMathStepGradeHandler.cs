using Core.DDD.Repositories;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.GradeReviews.Shared;
using Elmanhg.Application.MathStepGrading.Shared;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.Realtime;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.MathStepGrading;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Units;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.GradeReviews.ReviewMathStepGrade;

public sealed class ReviewMathStepGradeHandler(IMathStepGradeRepository mathStepGradeRepository, ISessionRepository sessionRepository, IQuestionMasteryRepository questionMasteryRepository, IQuestionRepository questionRepository, ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository, IOptions<MasteryOptions> masteryOptions, IGradeReviewNotifier gradeReviewNotifier, TimeProvider timeProvider, ICurrentUserService currentUserService) : IRequestHandler<ReviewMathStepGradeCommand, GradeReviewDetailResult>
{
    public async Task<GradeReviewDetailResult> Handle(ReviewMathStepGradeCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);
        var grade = await mathStepGradeRepository.GetRequiredAsync(x => x.Id == request.MathStepGradeId && x.SubjectId == request.SubjectId, ErrorCodes.GradeReviewNotFound, cancellationToken).ConfigureAwait(false);
        await GradeReviewSessionGuard.EnsureNotTestModeAsync(grade.SessionId, sessionRepository, cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();
        if (request.Decision == GradeReviewDecision.Accepted)
        {
            grade.Accept(userId, request.Comment, now);
        }
        else
        {
            grade.Override(request.Score!.Value, userId, request.Comment, now);
        }

        await MathStepAttemptRecorder.RecordAsync(grade, sessionRepository, questionMasteryRepository, masteryOptions.Value.CorrectThreshold, now, cancellationToken).ConfigureAwait(false);
        grade.MarkApplied(now);

        await mathStepGradeRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await gradeReviewNotifier.NotifyReviewedAsync(grade.StudentId, grade.SessionId, grade.QuestionId, cancellationToken).ConfigureAwait(false);

        var snapshot = await GradeReviewRevisionLoader.LoadAsync(grade.QuestionId, grade.QuestionVersion, questionRepository, cancellationToken).ConfigureAwait(false);
        var (_, placements) = await GradeReviewPlacementLoader.LoadAsync([grade.QuestionId], questionRepository, lessonRepository, unitRepository, cancellationToken).ConfigureAwait(false);
        return GradeReviewDetailGenerator.MathSteps(grade, snapshot, placements.GetValueOrDefault(grade.QuestionId) ?? GradeReviewPlacement.Unknown);
    }
}
