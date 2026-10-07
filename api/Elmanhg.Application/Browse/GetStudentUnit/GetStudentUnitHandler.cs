using Core.DDD.Repositories;
using Core.Identity.Tokens.CurrentUser;
using Core.Settings;
using Elmanhg.Application.Browse.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Domain.Units;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Browse.GetStudentUnit;

public sealed class GetStudentUnitHandler(ICurriculumUnitRepository unitRepository, ISubjectRepository subjectRepository, ILessonRepository lessonRepository, IQuestionMasteryRepository questionMasteryRepository, ISessionRepository sessionRepository, ISubscriptionRepository subscriptionRepository, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider, ICurrentUserService currentUserService, IRuntimeSettings runtimeSettings) : IRequestHandler<GetStudentUnitQuery, StudentUnitResult>
{
    public async Task<StudentUnitResult> Handle(GetStudentUnitQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);
        var unit = await unitRepository.GetRequiredAsync(request.UnitId, ErrorCodes.UnitNotFound, cancellationToken, asNoTracking: true).ConfigureAwait(false);

        var subject = await subjectRepository.GetRequiredAsync(unit.SubjectId, ErrorCodes.UnitNotFound, cancellationToken, asNoTracking: true).ConfigureAwait(false);

        var lessons = await lessonRepository.FindAsync(x => x.UnitId == unit.Id && x.State == LessonState.Published, cancellationToken, orderBy: query => query.OrderBy(x => x.Order).ThenBy(x => x.CreationDate), asNoTracking: true).ConfigureAwait(false);
        var entitlement = await StudentEntitlementLoader.LoadAsync(subscriptionRepository, runtimeSettings, userId, subscriptionsOptions.Value, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        var openLessonIds = LessonAccess.OpenLessonIds(lessons.Select(LessonPosition.Of), entitlement.OpenLessonsPerUnit);
        var counts = await questionMasteryRepository.GetLessonCountsAsync(userId, unit.SubjectId, cancellationToken).ConfigureAwait(false);
        var bests = await sessionRepository.GetBestExamScoresAsync(userId, cancellationToken).ConfigureAwait(false);
        return StudentUnitResultGenerator.Generate(unit, subject, lessons, counts, bests, openLessonIds);
    }
}
