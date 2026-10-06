using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Core.Queues;

public static class DependencyInjection
{
    public static IServiceCollection AddCoreBackgroundJobMetrics(this IServiceCollection services, string meterName, string metricPrefix, ActivitySource activitySource)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(provider => new BackgroundJobMetrics(provider.GetRequiredService<IMeterFactory>(), provider.GetRequiredService<TimeProvider>(), meterName, metricPrefix, activitySource));
        return services;
    }
}
