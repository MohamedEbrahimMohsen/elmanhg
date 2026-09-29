using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.TeacherThreads;

namespace Elmanhg.Application.TeacherThreads.Shared;

public static class AskTeacherGate
{
    public static (DateTimeOffset Start, DateTimeOffset End) CurrentQuotaMonth(DateTimeOffset now, string timeZone)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        var local = TimeZoneInfo.ConvertTime(now, zone);
        var start = MonthStart(local.Year, local.Month, zone);
        var end = local.Month == 12 ? MonthStart(local.Year + 1, 1, zone) : MonthStart(local.Year, local.Month + 1, zone);
        return (start, end);
    }

    public static async Task<int> CountQuestionsThisMonthAsync(Guid studentId, ITeacherThreadRepository teacherThreadRepository, SubscriptionsOptions options, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var (start, end) = CurrentQuotaMonth(now, options.DailyQuotaTimeZone);
        return await teacherThreadRepository.CountAsync(cancellationToken, x => x.StudentId == studentId && x.SubmittedAt >= start && x.SubmittedAt < end).ConfigureAwait(false);
    }

    public static async Task EnsureCanAskAsync(EntitlementResult entitlement, Guid studentId, ITeacherThreadRepository teacherThreadRepository, SubscriptionsOptions options, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!entitlement.HasAskTeacher)
        {
            throw new ForbiddenCoreException(ErrorCodes.AskTeacherRequiresSubscription);
        }

        var used = await CountQuestionsThisMonthAsync(studentId, teacherThreadRepository, options, now, cancellationToken).ConfigureAwait(false);
        if (used >= entitlement.MonthlyAskTeacherQuestionLimit)
        {
            throw new ForbiddenCoreException(ErrorCodes.AskTeacherMonthlyLimitReached, context: new Dictionary<string, object> { ["limit"] = entitlement.MonthlyAskTeacherQuestionLimit });
        }
    }

    private static DateTimeOffset MonthStart(int year, int month, TimeZoneInfo zone)
    {
        var wallClock = new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero);
        var offset = zone.GetUtcOffset(wallClock - zone.GetUtcOffset(wallClock));
        // Npgsql writes only UTC offsets to timestamptz, so the bound is returned in UTC.
        return (wallClock - offset).ToUniversalTime();
    }
}
