using Core.Identity;
using Core.Identity.Tokens.RefreshToken;
using Elmanhg.Domain.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Core.Identity;

public sealed class RefreshTokenRotationRegistrationTests
{
    private readonly ServiceCollection _services = new();

    [Fact]
    public void AddCoreRefreshTokenRotation_Always_RegistersScopedRotator()
    {
        _services.AddCoreRefreshTokenRotation<User>();

        var descriptor = _services.Single(x => x.ServiceType == typeof(IRefreshTokenRotator<User>));
        (descriptor.ImplementationType, descriptor.Lifetime).Should().Be((typeof(RefreshTokenRotator<User>), ServiceLifetime.Scoped));
    }

    [Fact]
    public void AddCoreRefreshTokenRotation_NoConfiguration_DefaultsGraceToTenSeconds()
    {
        _services.AddCoreRefreshTokenRotation<User>();
        using var provider = _services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<RefreshTokenRotationOptions>>().Value;

        options.ReuseGrace.Should().Be(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void AddCoreRefreshTokenRotation_NegativeGrace_FailsValidation()
    {
        _services.AddCoreRefreshTokenRotation<User>();
        _services.Configure<RefreshTokenRotationOptions>(x => x.ReuseGrace = TimeSpan.FromSeconds(-1));
        using var provider = _services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<IOptions<RefreshTokenRotationOptions>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }
}
