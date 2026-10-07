using Core.EntityFrameworkCore;
using Core.EntityFrameworkCore.Auditing;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Core.Persistence;

public sealed class AuditStampingRegistrationTests
{
    [Fact]
    public void AddCoreAuditStamping_Always_RegistersScopedInterceptor()
    {
        var services = Services();

        services.Single(x => x.ServiceType == typeof(AuditStampingInterceptor)).Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddCoreAuditStamping_ResolvedContext_UsesTheScopeInterceptor()
    {
        using var provider = Services().BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AuditStampingRegistrationProbeDbContext>();

        var interceptors = context.GetService<IDbContextOptions>().FindExtension<CoreOptionsExtension>()?.Interceptors ?? [];

        interceptors.OfType<AuditStampingInterceptor>().Should().ContainSingle()
            .Which.Should().BeSameAs(scope.ServiceProvider.GetRequiredService<AuditStampingInterceptor>());
    }

    private static ServiceCollection Services()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AuditStampingRegistrationProbeDbContext>(o => o.UseNpgsql("Host=localhost;Database=audit-stamping-registration"));
        services.AddCoreAuditStamping<AuditStampingRegistrationProbeDbContext>();
        return services;
    }
}
