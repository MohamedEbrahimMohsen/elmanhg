using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Subjects.Shared;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using MediatR;
using System.Security.Claims;

namespace Elmanhg.Application.Subjects.GetSubject;

public sealed class GetSubjectHandler(ISubjectRepository subjectRepository, ICurriculumUnitRepository unitRepository, ILessonRepository lessonRepository, ICurrentUserService currentUserService) : IRequestHandler<GetSubjectQuery, SubjectDetailResult>
{
    public async Task<SubjectDetailResult> Handle(GetSubjectQuery request, CancellationToken cancellationToken)
    {
        var subject = await subjectRepository.GetByIdAsync(request.SubjectId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        if (subject is null)
        {
            throw new NotFoundCoreException(ErrorCodes.SubjectNotFound);
        }

        var units = await unitRepository.FindAsync(x => x.SubjectId == subject.Id, cancellationToken, orderBy: query => query.OrderBy(x => x.Order).ThenBy(x => x.CreationDate), asNoTracking: true).ConfigureAwait(false);
        var publishedOnly = currentUserService.GetClaim(ClaimTypes.Role) != nameof(UserRole.Admin);
        var lessonCounts = await lessonRepository.CountByUnitAsync(units.Select(x => x.Id).ToList(), publishedOnly, cancellationToken).ConfigureAwait(false);

        return SubjectResultGenerator.GenerateDetail(subject, units, lessonCounts);
    }
}
