using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.Metrics;

namespace Elmanhg.Tests.Application.Features.Shared.Observability;

internal static class MeterFactories
{
    public static IMeterFactory Create() => new ServiceCollection().AddMetrics().BuildServiceProvider().GetRequiredService<IMeterFactory>();
}
