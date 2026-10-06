using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics.Metrics;

namespace Core.Observability;

public static class DependencyInjection
{
    private const string EnvironmentAttribute = "deployment.environment.name";

    public static IServiceCollection AddCoreObservability(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment, TelemetrySetup setup)
    {
        services.AddOptions<ObservabilityOptions>().Configure(x => x.ServiceName = setup.DefaultServiceName).BindConfiguration(ObservabilityOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IValidateOptions<ObservabilityOptions>, ObservabilityOptionsValidator>();

        var options = new ObservabilityOptions { ServiceName = setup.DefaultServiceName };
        configuration.GetSection(ObservabilityOptions.SectionName).Bind(options);
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(options.ServiceName, serviceVersion: options.ServiceVersion).AddAttributes([new(EnvironmentAttribute, environment.EnvironmentName)]))
            .WithTracing(tracing => TelemetryProviders.ConfigureTracing(tracing, options, setup))
            .WithMetrics(metrics => TelemetryProviders.ConfigureMetrics(metrics, options, setup));

        return services;
    }

    public static IServiceCollection AddCoreRequestMetrics(this IServiceCollection services, string meterName, string metricPrefix)
    {
        services.AddSingleton(provider => new RequestMetrics(provider.GetRequiredService<IMeterFactory>(), meterName, metricPrefix));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(RequestMetricsBehaviour<,>));
        return services;
    }
}
