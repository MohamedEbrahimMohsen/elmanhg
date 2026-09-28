using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Subjects.Shared;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Teachers;
using Elmanhg.Domain.Units;
using MediatR;
using System.Security.Claims;

namespace Elmanhg.Application.Subjects.GetSubjects;

public sealed class GetSubjectsHandler(ISubjectRepository subjectRepository, ITeacherSubjectRepository teacherSubjectRepository, ICurriculumUnitRepository unitRepository, ICurrentUserService currentUserService) : IRequestHandler<GetSubjectsQuery, List<SubjectResult>>
{
    public async Task<List<SubjectResult>> Handle(GetSubjectsQuery request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        List<Subject> subjects;
        if (currentUserService.GetClaim(ClaimTypes.Role) == nameof(UserRole.Teacher))
        {
            var userId = currentUserService.UserId.Value;
            var assignedIds = (await teacherSubjectRepository.FindAsync(x => x.TeacherId == userId, cancellationToken, asNoTracking: true).ConfigureAwait(false))
                .Select(x => x.SubjectId)
                .ToList();
            subjects = await subjectRepository.FindAsync(x => assignedIds.Contains(x.Id), cancellationToken, orderBy: query => query.OrderBy(x => x.Order).ThenBy(x => x.CreationDate), asNoTracking: true).ConfigureAwait(false);
        }
        else
        {
            subjects = await subjectRepository.GetAllAsync(cancellationToken, orderBy: query => query.OrderBy(x => x.Order).ThenBy(x => x.CreationDate), asNoTracking: true).ConfigureAwait(false) ?? [];
        }

        var counts = await unitRepository.CountBySubjectAsync(subjects.Select(x => x.Id).ToList(), cancellationToken).ConfigureAwait(false);

        return subjects
            .Select(x => SubjectResultGenerator.Generate(x, counts.GetValueOrDefault(x.Id)))
            .ToList();
    }
}
