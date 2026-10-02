using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Browse.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.RuntimeSettings;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Domain.Units;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Browse.GetStudentLesson;

public sealed class GetStudentLessonHandler(ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository, ISubjectRepository subjectRepository, IQuestionMasteryRepository questionMasteryRepository, ISubscriptionRepository subscriptionRepository, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider, ICurrentUserService currentUserService, IRuntimeSettings runtimeSettings) : IRequestHandler<GetStudentLessonQuery, StudentLessonResult>
{
    public async Task<StudentLessonResult> Handle(GetStudentLessonQuery request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var userId = currentUserService.UserId.Value;
        var lesson = await lessonRepository.GetWithObjectivesAsync(request.LessonId, asNoTracking: true, cancellationToken).ConfigureAwait(false);
        if (lesson is null || lesson.State != LessonState.Published)
        {
            throw new NotFoundCoreException(ErrorCodes.LessonNotFound);
        }

        var unit = await unitRepository.GetByIdAsync(lesson.UnitId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        if (unit is null)
        {
            throw new NotFoundCoreException(ErrorCodes.LessonNotFound);
        }

        var subject = await subjectRepository.GetByIdAsync(unit.SubjectId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        if (subject is null)
        {
            throw new NotFoundCoreException(ErrorCodes.LessonNotFound);
        }

        var units = await unitRepository.FindAsync(x => x.SubjectId == subject.Id, cancellationToken, orderBy: query => query.OrderBy(x => x.Order).ThenBy(x => x.CreationDate), asNoTracking: true).ConfigureAwait(false);
        var unitIds = units.Select(x => x.Id).ToList();
        var lessons = await lessonRepository.FindAsync(x => unitIds.Contains(x.UnitId) && x.State == LessonState.Published, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var entitlement = await StudentEntitlementLoader.LoadAsync(subscriptionRepository, runtimeSettings, userId, subscriptionsOptions.Value, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        var isLocked = !LessonAccess.IsOpen(lesson.Id, lessons.Select(LessonPosition.Of), entitlement.OpenLessonsPerUnit);
        var counts = await questionMasteryRepository.GetLessonCountsAsync(userId, subject.Id, cancellationToken).ConfigureAwait(false);
        return StudentLessonResultGenerator.Generate(lesson, unit, subject, units, lessons, counts, isLocked);
    }
}
