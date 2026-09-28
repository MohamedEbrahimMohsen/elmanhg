using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.QuestionValidation.Shared;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Teachers;
using Elmanhg.Domain.Units;
using MediatR;

namespace Elmanhg.Application.QuestionValidation.GetValidationQueueFilters;

public sealed class GetValidationQueueFiltersHandler(ITeacherSubjectRepository teacherSubjectRepository, ISubjectRepository subjectRepository, ICurriculumUnitRepository unitRepository, ILessonRepository lessonRepository, ICurrentUserService currentUserService) : IRequestHandler<GetValidationQueueFiltersQuery, ValidationQueueFiltersResult>
{
    public async Task<ValidationQueueFiltersResult> Handle(GetValidationQueueFiltersQuery request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var userId = currentUserService.UserId.Value;
        var assignments = await teacherSubjectRepository.FindAsync(x => x.TeacherId == userId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var subjectIds = assignments
            .Select(x => x.SubjectId)
            .ToList();
        var subjects = await subjectRepository.FindAsync(x => subjectIds.Contains(x.Id), cancellationToken, orderBy: query => query.OrderBy(x => x.Order).ThenBy(x => x.CreationDate), asNoTracking: true).ConfigureAwait(false);
        var units = await unitRepository.FindAsync(x => subjectIds.Contains(x.SubjectId), cancellationToken, orderBy: query => query.OrderBy(x => x.Order), asNoTracking: true).ConfigureAwait(false);
        var unitIds = units
            .Select(x => x.Id)
            .ToList();
        var lessons = await lessonRepository.FindAsync(x => unitIds.Contains(x.UnitId), cancellationToken, orderBy: query => query.OrderBy(x => x.Order), asNoTracking: true).ConfigureAwait(false);

        var subjectOptions = subjects
            .Select(x => new ValidationSubjectOption(x.Id, x.Name))
            .ToList();
        var unitOptions = units
            .Select(x => new ValidationUnitOption(x.Id, x.SubjectId, x.Name))
            .ToList();
        var lessonOptions = lessons
            .Select(x => new ValidationLessonOption(x.Id, x.UnitId, x.Name))
            .ToList();
        return new ValidationQueueFiltersResult(subjectOptions, unitOptions, lessonOptions);
    }
}
