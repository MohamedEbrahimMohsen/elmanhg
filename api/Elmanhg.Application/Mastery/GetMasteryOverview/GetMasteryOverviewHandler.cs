using Core.DDD.Time;
using Core.Identity.Tokens.CurrentUser;
using Core.Settings;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Mastery.Shared;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Subscriptions;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Mastery.GetMasteryOverview;

public sealed class GetMasteryOverviewHandler(IQuestionMasteryRepository questionMasteryRepository, ISessionRepository sessionRepository, ISubjectRepository subjectRepository, ILessonRepository lessonRepository, IUserRepository userRepository, ISubscriptionRepository subscriptionRepository, IOptions<ProgressOptions> progressOptions, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider, ICurrentUserService currentUserService, IRuntimeSettings runtimeSettings) : IRequestHandler<GetMasteryOverviewQuery, MasteryOverviewResult>
{
    public async Task<MasteryOverviewResult> Handle(GetMasteryOverviewQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);
        var student = await userRepository.GetByIdAsync(userId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var lessons = await questionMasteryRepository.GetLessonCountsAsync(userId, null, cancellationToken).ConfigureAwait(false);
        var subjects = await subjectRepository.GetAllAsync(cancellationToken, orderBy: query => query.OrderBy(x => x.Order).ThenBy(x => x.CreationDate), asNoTracking: true).ConfigureAwait(false) ?? [];

        var options = progressOptions.Value;
        var now = timeProvider.GetUtcNow();
        var today = TimeZoneInfo.FindSystemTimeZoneById(options.StreakTimeZone).LocalDate(now);
        var activeDays = await sessionRepository.GetQuizActivityDaysAsync(userId, options.StreakTimeZone, now.AddDays(-options.StreakMaxDays), cancellationToken).ConfigureAwait(false);

        var entitlement = await StudentEntitlementLoader.LoadAsync(subscriptionRepository, runtimeSettings, userId, subscriptionsOptions.Value, now, cancellationToken).ConfigureAwait(false);
        var candidates = entitlement.OpenLessonsPerUnit is null ? lessons : await OpenLessonsAsync(lessons, entitlement.OpenLessonsPerUnit, cancellationToken).ConfigureAwait(false);
        var next = NextLessonRecommendation.Pick(candidates);
        var nextLesson = next is null ? null : await lessonRepository.GetByIdAsync(next.LessonId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        return MasteryOverviewResultGenerator.Generate(lessons, subjects, student?.SubjectInterestIds ?? [], StudyStreak.Count(activeDays, today), next, nextLesson);
    }

    private async Task<List<LessonMasteryCount>> OpenLessonsAsync(List<LessonMasteryCount> lessons, int? openLessonsPerUnit, CancellationToken cancellationToken)
    {
        var open = LessonAccess.OpenLessonIds(await lessonRepository.GetPublishedPositionsAsync(cancellationToken).ConfigureAwait(false), openLessonsPerUnit);
        return lessons
            .Where(x => open.Contains(x.LessonId))
            .ToList();
    }
}
