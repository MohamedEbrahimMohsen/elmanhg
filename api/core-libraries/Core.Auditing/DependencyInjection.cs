using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Core.Auditing;

public static class DependencyInjection
{
    public static IServiceCollection AddCoreAuditing(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAuditChangeCollector, AuditChangeCollector>();
        services.TryAddSingleton(TimeProvider.System);

        var options = configuration.GetSection(AuditOptions.SectionName).Get<AuditOptions>() ?? new AuditOptions();

        if (options.Enabled)
        {
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuditBehaviour<,>));
        }

        return services;
    }
}
