using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Browse.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using MediatR;

namespace Elmanhg.Application.Browse.GetStudentSubject;

public sealed class GetStudentSubjectHandler(ISubjectRepository subjectRepository, ICurriculumUnitRepository unitRepository, ILessonRepository lessonRepository, IQuestionMasteryRepository questionMasteryRepository, ISessionRepository sessionRepository, ICurrentUserService currentUserService) : IRequestHandler<GetStudentSubjectQuery, StudentSubjectResult>
{
    public async Task<StudentSubjectResult> Handle(GetStudentSubjectQuery request, CancellationToken cancellationToken)
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
        var lessonCounts = await lessonRepository.CountByUnitAsync(unitIds, publishedOnly: true, cancellationToken).ConfigureAwait(false);
        var counts = await questionMasteryRepository.GetLessonCountsAsync(userId, subject.Id, cancellationToken).ConfigureAwait(false);
        var bests = await sessionRepository.GetBestExamScoresAsync(userId, cancellationToken).ConfigureAwait(false);
        return StudentSubjectResultGenerator.Generate(subject, units, lessonCounts, counts, bests);
    }
}
