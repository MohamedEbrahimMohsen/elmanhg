using Core.Errors;
using Core.Utilities.Time;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Sessions;

namespace Elmanhg.Application.Subscriptions.Shared;

public static class FreeTierGate
{
    public static void EnsureCanTakeExams(EntitlementResult entitlement)
    {
        if (!entitlement.CanTakeExams)
        {
            throw new ForbiddenCoreException(ErrorCodes.ExamRequiresSubscription);
        }
    }

    public static async Task EnsureLessonOpenAsync(EntitlementResult entitlement, Guid lessonId, ILessonRepository lessonRepository, CancellationToken cancellationToken)
    {
        if (entitlement.OpenLessonsPerUnit is null)
        {
            return;
        }

        var siblings = await lessonRepository.GetPublishedSiblingPositionsAsync(lessonId, cancellationToken).ConfigureAwait(false);
        if (!LessonAccess.IsOpen(lessonId, siblings, entitlement.OpenLessonsPerUnit))
        {
            throw new ForbiddenCoreException(ErrorCodes.LessonLocked);
        }
    }

    public static async Task<int> CountQuizQuestionsTodayAsync(Guid studentId, ISessionRepository sessionRepository, SubscriptionsOptions options, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var today = TimeZoneInfo.FindSystemTimeZoneById(options.DailyQuotaTimeZone).LocalDate(now);
        return await sessionRepository.CountQuizAttemptsOnDayAsync(studentId, options.DailyQuotaTimeZone, today, cancellationToken).ConfigureAwait(false);
    }

    public static async Task EnsureQuizQuestionAvailableAsync(EntitlementResult entitlement, Guid studentId, ISessionRepository sessionRepository, SubscriptionsOptions options, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (entitlement.DailyQuizQuestionLimit is not { } limit)
        {
            return;
        }

        var used = await CountQuizQuestionsTodayAsync(studentId, sessionRepository, options, now, cancellationToken).ConfigureAwait(false);
        if (used >= limit)
        {
            throw new ForbiddenCoreException(ErrorCodes.QuizDailyLimitReached, context: new Dictionary<string, object> { ["limit"] = limit });
        }
    }
}
