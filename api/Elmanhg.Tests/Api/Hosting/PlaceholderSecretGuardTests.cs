using Elmanhg.Api.Hosting;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Elmanhg.Tests.Api.Hosting;

public sealed class PlaceholderSecretGuardTests
{
    private const string RealOtpSecret = "not-a-secret-otp-0f1e2d3c4b5a6978";

    public static TheoryData<string> SecretKeys => [.. PlaceholderSecretGuard.SecretKeys];

    [Fact]
    public void EnsureReplaced_ProductionWithPlaceholderJwtKey_ThrowsNamingTheKey()
    {
        var act = () => PlaceholderSecretGuard.EnsureReplaced(Configuration(("CoreJwt:Key", "change-me-openssl-rand-base64-48")), Environment(Environments.Production));

        act.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("CoreJwt:Key");
    }

    [Fact]
    public void EnsureReplaced_StagingWithPlaceholderAdminPassword_Throws()
    {
        var act = () => PlaceholderSecretGuard.EnsureReplaced(Configuration(("AdminSeed:Password", "change-me-Admin1")), Environment(Environments.Staging));

        act.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("AdminSeed:Password");
    }

    [Fact]
    public void EnsureReplaced_ProductionWithPlaceholderDatabasePassword_ThrowsNamingConnectionString()
    {
        var act = () => PlaceholderSecretGuard.EnsureReplaced(Configuration(("ConnectionStrings:DbConnectionString", "Host=db;Password=change-me-openssl-rand-hex-32")), Environment(Environments.Production));

        act.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("ConnectionStrings:DbConnectionString");
    }

    [Fact]
    public void EnsureReplaced_ProductionWithEmptyOtpSecret_ThrowsNamingOtpSecret()
    {
        var act = () => PlaceholderSecretGuard.EnsureReplaced(new ConfigurationBuilder().Build(), Environment(Environments.Production));

        act.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("CoreOtp:Secret");
    }

    [Fact]
    public void EnsureReplaced_DevelopmentWithPlaceholders_DoesNotThrow()
    {
        var placeholders = PlaceholderSecretGuard.SecretKeys
            .Select(x => (x, "change-me"))
            .Append(("ConnectionStrings:DbConnectionString", "Host=db;Password=change-me"))
            .ToArray();

        var act = () => PlaceholderSecretGuard.EnsureReplaced(new ConfigurationBuilder().AddInMemoryCollection(placeholders.Select(x => new KeyValuePair<string, string?>(x.Item1, x.Item2))).Build(), Environment(Environments.Development));

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureReplaced_ProductionWithRealSecrets_DoesNotThrow()
    {
        var act = () => PlaceholderSecretGuard.EnsureReplaced(Configuration(("CoreJwt:Key", "not-a-secret-jwt-key-k2VqB8xR0dLm4pW7"), ("ConnectionStrings:DbConnectionString", "Host=db;Password=not-a-secret-9c8b7a")), Environment(Environments.Production));

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureReplaced_PlaceholderValue_IsNeverInTheMessage()
    {
        var act = () => PlaceholderSecretGuard.EnsureReplaced(Configuration(("AiService:ServiceToken", "change-me-UNIQUE-4711")), Environment(Environments.Production));

        act.Should().Throw<InvalidOperationException>().Which.Message.Should().NotContain("UNIQUE-4711");
    }

    [Theory]
    [MemberData(nameof(SecretKeys))]
    public void EnsureReplaced_EverySecretKeyWithPlaceholder_Throws(string key)
    {
        var act = () => PlaceholderSecretGuard.EnsureReplaced(Configuration((key, "change-me-value")), Environment(Environments.Production));

        act.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain(key);
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] settings)
    {
        var values = settings
            .Select(x => new KeyValuePair<string, string?>(x.Key, x.Value))
            .Prepend(new KeyValuePair<string, string?>("CoreOtp:Secret", RealOtpSecret))
            .GroupBy(x => x.Key)
            .Select(x => x.Last());
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static IHostEnvironment Environment(string name)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(name);
        return environment;
    }
}
