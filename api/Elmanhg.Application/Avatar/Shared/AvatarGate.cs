using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.Avatar;
using Elmanhg.Domain.Sessions;

namespace Elmanhg.Application.Avatar.Shared;

public static class AvatarGate
{
    public static async Task EnsureNoExamInProgressAsync(Guid studentId, ISessionRepository sessionRepository, ExamsOptions examsOptions, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await IsExamInProgressAsync(studentId, sessionRepository, examsOptions, now, cancellationToken).ConfigureAwait(false))
        {
            throw new ForbiddenCoreException(ErrorCodes.AvatarExamInProgress);
        }
    }

    public static async Task<bool> IsExamInProgressAsync(Guid studentId, ISessionRepository sessionRepository, ExamsOptions examsOptions, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var session = await sessionRepository.FirstOrDefaultAsync(InProgressExamSpecification.For(studentId, now, examsOptions.DeadlineGrace), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        return session is not null;
    }

    public static async Task<int> CountMessagesTodayAsync(Guid studentId, IAvatarMessageUsageRepository repository, SubscriptionsOptions options, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(options.DailyQuotaTimeZone);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        return await repository.CountOnDayAsync(studentId, options.DailyQuotaTimeZone, today, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<int> EnsureMessageAvailableAsync(EntitlementResult entitlement, Guid studentId, IAvatarMessageUsageRepository repository, SubscriptionsOptions options, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var used = await CountMessagesTodayAsync(studentId, repository, options, now, cancellationToken).ConfigureAwait(false);
        if (used >= entitlement.DailyAvatarMessageLimit)
        {
            throw new ForbiddenCoreException(ErrorCodes.AvatarDailyLimitReached, context: new Dictionary<string, object> { ["limit"] = entitlement.DailyAvatarMessageLimit });
        }

        return used;
    }
}
