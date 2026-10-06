using Core.Observability;
using Elmanhg.Application.Sessions.Shared;
using Elmanhg.Application.Sessions.SubmitAnswer;
using Elmanhg.Application.Shared.Observability;
using Elmanhg.Application.Subscriptions.ProcessPaymentNotification;
using Elmanhg.Application.Subscriptions.Shared;
using MediatR;

namespace Elmanhg.Api.Hosting;

public static class ObservabilityExtensions
{
    private const string DefaultServiceName = "elmanhg-api";
    private const string MetricPrefix = "elmanhg";
    private const string NpgsqlSourceName = "Npgsql";

    public static IServiceCollection AddElmanhgObservability(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddSingleton<ElmanhgMetrics>();
        services.AddSingleton<BackgroundJobMetrics>();
        services.AddCoreRequestMetrics(ElmanhgTelemetry.SourceName, MetricPrefix);
        services.AddTransient<IPipelineBehavior<SubmitAnswerCommand, SessionItemResult>, QuizAnswerMetricsBehaviour>();
        services.AddTransient<IPipelineBehavior<ProcessPaymentNotificationCommand, PaymentNotificationResult>, PaymentNotificationMetricsBehaviour>();
        return services.AddCoreObservability(configuration, environment, new TelemetrySetup(DefaultServiceName, [ElmanhgTelemetry.SourceName, NpgsqlSourceName], [ElmanhgTelemetry.SourceName]));
    }
}
