using Core.Errors;
using Core.Identity;
using Core.Identity.Exceptions;
using Core.Identity.Tokens;
using Core.Identity.Tokens.RefreshToken;
using Elmanhg.Domain.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Core.Identity;

public sealed class RefreshTokenRotationRegistrationTests : RefreshTokenRotatorTestBase
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
    public async Task AddCoreRefreshTokenRotation_NoConfiguration_DefaultsGraceToTenSeconds()
    {
        Record(OldRefreshToken, Guid.NewGuid(), rotatedAt: Now.AddSeconds(-10));
        await using var provider = RegisteredProvider();
        await using var scope = provider.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<IRefreshTokenRotator<User>>().RotateAsync(_user, OldRefreshToken, TestContext.Current.CancellationToken);

        result.Should().Be(NewRefreshToken);
        _records.Should().NotContain(x => x.IsRevoked);
    }

    [Fact]
    public async Task AddCoreRefreshTokenRotation_NoConfiguration_RevokesReplayAfterTenSeconds()
    {
        var replayed = Record(OldRefreshToken, Guid.NewGuid(), rotatedAt: Now.AddSeconds(-11));
        await using var provider = RegisteredProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => scope.ServiceProvider.GetRequiredService<IRefreshTokenRotator<User>>().RotateAsync(_user, OldRefreshToken, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.RefreshTokenRevoked);
        replayed.IsRevoked.Should().BeTrue();
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

    private ServiceProvider RegisteredProvider()
    {
        _services.AddSingleton(_refreshTokenService);
        _services.AddSingleton(_issuedRefreshTokenRepository);
        _services.AddSingleton(_timeProvider);
        _services.AddSingleton(Options.Create(new JwtOptions { RefreshTokenExpirationDays = 7 }));
        _services.AddCoreRefreshTokenRotation<User>();
        return _services.BuildServiceProvider();
    }
}
