using Elmanhg.Application.Sessions.Shared;
using Elmanhg.Application.Sessions.SubmitAnswer;
using Elmanhg.Application.Shared.Observability;
using Elmanhg.Application.Subscriptions.ProcessPaymentNotification;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Infrastructure.Hosting;
using MediatR;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Elmanhg.Api.Hosting;

public static class ObservabilityExtensions
{
    private const string HealthPath = "/health";
    private const string NpgsqlSourceName = "Npgsql";
    private const string RuntimeMeterName = "System.Runtime";
    private const int MillisecondsPerSecond = 1000;

    public static IServiceCollection AddElmanhgObservability(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddOptions<ObservabilityOptions>().BindConfiguration(ObservabilityOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IValidateOptions<ObservabilityOptions>, ObservabilityOptionsValidator>();
        services.AddSingleton<ElmanhgMetrics>();
        services.AddSingleton<BackgroundJobMetrics>();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(RequestMetricsBehaviour<,>));
        services.AddTransient<IPipelineBehavior<SubmitAnswerCommand, SessionItemResult>, QuizAnswerMetricsBehaviour>();
        services.AddTransient<IPipelineBehavior<ProcessPaymentNotificationCommand, PaymentNotificationResult>, PaymentNotificationMetricsBehaviour>();

        var options = configuration.GetSection(ObservabilityOptions.SectionName).Get<ObservabilityOptions>() ?? new ObservabilityOptions();
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(options.ServiceName, serviceVersion: options.ServiceVersion).AddAttributes([new("deployment.environment.name", environment.EnvironmentName)]))
            .WithTracing(tracing => ConfigureTracing(tracing, options))
            .WithMetrics(metrics => ConfigureMetrics(metrics, options));

        return services;
    }

    private static void ConfigureTracing(TracerProviderBuilder tracing, ObservabilityOptions options)
    {
        tracing
            .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(options.TraceSampleRatio)))
            .AddAspNetCoreInstrumentation(x => x.Filter = context => !context.Request.Path.StartsWithSegments(HealthPath))
            .AddHttpClientInstrumentation(x => x.RecordException = true)
            .AddSource(ElmanhgTelemetry.SourceName)
            .AddSource(NpgsqlSourceName);
        if (options.ExportEndpoint is { } endpoint)
        {
            tracing.AddOtlpExporter(exporter => Configure(exporter, options, endpoint));
        }
    }

    private static void ConfigureMetrics(MeterProviderBuilder metrics, ObservabilityOptions options)
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddMeter(ElmanhgTelemetry.SourceName)
            .AddMeter(RuntimeMeterName);
        if (options.ExportEndpoint is { } endpoint)
        {
            metrics.AddOtlpExporter((exporter, reader) =>
            {
                Configure(exporter, options, endpoint);
                reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = options.MetricExportIntervalSeconds * MillisecondsPerSecond;
            });
        }
    }

    private static void Configure(OtlpExporterOptions exporter, ObservabilityOptions options, Uri endpoint)
    {
        exporter.Endpoint = endpoint;
        exporter.Protocol = OtlpExportProtocol.Grpc;
        if (!string.IsNullOrWhiteSpace(options.OtlpHeaders))
        {
            exporter.Headers = options.OtlpHeaders;
        }
    }
}
