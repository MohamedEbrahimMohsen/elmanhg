using Core.Queues;
using Elmanhg.Application.Shared.Observability;
using System.Diagnostics.Metrics;

namespace Elmanhg.Tests.Api.Workers;

internal static class ElmanhgJobMetrics
{
    public const string JobTag = "elmanhg.job";
    public const string OutcomeTag = "elmanhg.outcome";

    public static BackgroundJobMetrics Create(IMeterFactory meterFactory, TimeProvider timeProvider) => new(meterFactory, timeProvider, ElmanhgTelemetry.SourceName, ElmanhgTelemetry.MetricPrefix, ElmanhgTelemetry.ActivitySource);
}
