using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Mastery.Shared;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using MediatR;

namespace Elmanhg.Application.Mastery.GetSubjectMastery;

public sealed class GetSubjectMasteryHandler(IQuestionMasteryRepository questionMasteryRepository, ISubjectRepository subjectRepository, ICurriculumUnitRepository unitRepository, ILessonRepository lessonRepository, ICurrentUserService currentUserService) : IRequestHandler<GetSubjectMasteryQuery, SubjectMasteryDetailResult>
{
    public async Task<SubjectMasteryDetailResult> Handle(GetSubjectMasteryQuery request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var userId = currentUserService.UserId.Value;
        var subject = await subjectRepository.GetByIdAsync(request.SubjectId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        if (subject is null)
        {
            throw new NotFoundCoreException(ErrorCodes.SubjectNotFound);
        }

        var units = await unitRepository.FindAsync(x => x.SubjectId == subject.Id, cancellationToken, orderBy: query => query.OrderBy(x => x.Order).ThenBy(x => x.CreationDate), asNoTracking: true).ConfigureAwait(false);
        var unitIds = units.Select(x => x.Id).ToList();
        var lessons = await lessonRepository.FindAsync(x => unitIds.Contains(x.UnitId) && x.State == LessonState.Published, cancellationToken, orderBy: query => query.OrderBy(x => x.Order).ThenBy(x => x.CreationDate), asNoTracking: true).ConfigureAwait(false);
        var counts = await questionMasteryRepository.GetLessonCountsAsync(userId, subject.Id, cancellationToken).ConfigureAwait(false);
        return SubjectMasteryResultGenerator.Generate(subject, units, lessons, counts);
    }
}
