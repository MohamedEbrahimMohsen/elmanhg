using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.GradeReviews.Shared;
using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.MathStepGrading;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Teachers;
using MediatR;
using System.Security.Claims;

namespace Elmanhg.Application.GradeReviews.GetGradeReviewSubjects;

public sealed class GetGradeReviewSubjectsHandler(ITeacherSubjectRepository teacherSubjectRepository, ISubjectRepository subjectRepository, IEssayGradeRepository essayGradeRepository, IMathStepGradeRepository mathStepGradeRepository, ICurrentUserService currentUserService) : IRequestHandler<GetGradeReviewSubjectsQuery, List<GradeReviewSubjectResult>>
{
    public async Task<List<GradeReviewSubjectResult>> Handle(GetGradeReviewSubjectsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);
        var isAdmin = currentUserService.GetClaim(ClaimTypes.Role) == nameof(UserRole.Admin);
        List<Guid>? subjectIds = null;
        List<Subject> subjects;
        if (isAdmin)
        {
            subjects = await subjectRepository.FindAsync(_ => true, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        }
        else
        {
            var assignments = await teacherSubjectRepository.FindAsync(x => x.TeacherId == userId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
            var ids = assignments
                .Select(x => x.SubjectId)
                .Distinct()
                .ToList();
            subjectIds = ids;
            subjects = await subjectRepository.FindAsync(x => ids.Contains(x.Id), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        }

        var essays = await essayGradeRepository.CountInReviewBySubjectAsync(subjectIds, cancellationToken).ConfigureAwait(false);
        var maths = await mathStepGradeRepository.CountInReviewBySubjectAsync(subjectIds, cancellationToken).ConfigureAwait(false);
        return subjects
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Name)
            .ThenBy(x => x.Id)
            .Select(x => new GradeReviewSubjectResult(x.Id, x.Name, essays.GetValueOrDefault(x.Id), maths.GetValueOrDefault(x.Id)))
            .ToList();
    }
}
