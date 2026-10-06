using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Core.Observability;

internal static class TelemetryProviders
{
    private const string RuntimeMeterName = "System.Runtime";
    private const int MillisecondsPerSecond = 1000;

    public static void ConfigureTracing(TracerProviderBuilder tracing, ObservabilityOptions options, TelemetrySetup setup)
    {
        tracing
            .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(options.TraceSampleRatio)))
            .AddAspNetCoreInstrumentation(x => x.Filter = context => !context.Request.Path.StartsWithSegments(setup.HealthPath))
            .AddHttpClientInstrumentation(x => x.RecordException = true)
            .AddSource([.. setup.ActivitySources]);
        if (options.ExportEndpoint is { } endpoint)
        {
            tracing.AddOtlpExporter(exporter => ConfigureExporter(exporter, options, endpoint));
        }
    }

    public static void ConfigureMetrics(MeterProviderBuilder metrics, ObservabilityOptions options, TelemetrySetup setup)
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddMeter([.. setup.Meters])
            .AddMeter(RuntimeMeterName);
        if (options.ExportEndpoint is { } endpoint)
        {
            metrics.AddOtlpExporter((exporter, reader) =>
            {
                ConfigureExporter(exporter, options, endpoint);
                reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = options.MetricExportIntervalSeconds * MillisecondsPerSecond;
            });
        }
    }

    private static void ConfigureExporter(OtlpExporterOptions exporter, ObservabilityOptions options, Uri endpoint)
    {
        exporter.Endpoint = endpoint;
        exporter.Protocol = OtlpExportProtocol.Grpc;
        if (!string.IsNullOrWhiteSpace(options.OtlpHeaders))
        {
            exporter.Headers = options.OtlpHeaders;
        }
    }
}
