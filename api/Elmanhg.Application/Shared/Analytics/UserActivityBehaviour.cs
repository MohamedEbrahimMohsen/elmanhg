using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Auth.Shared;
using Elmanhg.Application.Dashboard.Shared;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Analytics;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace Elmanhg.Application.Shared.Analytics;

public sealed class UserActivityBehaviour<TRequest, TResponse>(ICurrentUserService currentUserService, IUserActivityDayRepository userActivityDayRepository, IMemoryCache memoryCache, TimeProvider timeProvider, IOptions<DashboardOptions> dashboardOptions, ILogger<UserActivityBehaviour<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    // One insert per user per day per instance; the unique index absorbs the rest.
    private static readonly TimeSpan SeenKeyLifetime = TimeSpan.FromDays(1);

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next(cancellationToken).ConfigureAwait(false);
        var userId = currentUserService.UserId is { } id && id != Guid.Empty ? id : (response as AuthResult)?.User.Id;
        if (userId is null)
        {
            return response;
        }

        var now = timeProvider.GetUtcNow();
        var day = DashboardWindow.LocalDay(now, dashboardOptions.Value.TimeZone);
        var key = string.Create(CultureInfo.InvariantCulture, $"user-activity:{userId.Value:N}:{day:yyyy-MM-dd}");
        if (memoryCache.TryGetValue(key, out _))
        {
            return response;
        }

        // Activity is telemetry: a failed write is logged and must never fail the request it describes.
        try
        {
            await userActivityDayRepository.AddIfAbsentAsync(UserActivityDay.Record(userId.Value, day, now), cancellationToken).ConfigureAwait(false);
            memoryCache.Set(key, true, SeenKeyLifetime);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not record activity for user {UserId} on {Day}.", userId, day);
        }

        return response;
    }
}
