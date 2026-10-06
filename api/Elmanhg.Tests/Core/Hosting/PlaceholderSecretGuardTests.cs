using Core.Hosting;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Elmanhg.Tests.Core.Hosting;

public sealed class PlaceholderSecretGuardTests
{
    private const string RequiredKey = "Sample:Required";

    private static readonly PlaceholderSecretRules Rules = RulesFor(["Sample:ApiKey", "Sample:Token"]);

    [Fact]
    public void EnsureReplaced_DevelopmentWithPlaceholders_DoesNotThrow()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection([new("Sample:ApiKey", "example-a"), new("Sample:Token", "example-b"), new("Sample:Connection", "bad")]).Build();

        var act = () => PlaceholderSecretGuard.EnsureReplaced(configuration, Environment(Environments.Development), Rules);

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureReplaced_PlaceholderSecretKey_ThrowsNamingKeyAndGuidance()
    {
        var act = () => PlaceholderSecretGuard.EnsureReplaced(Configuration(("Sample:ApiKey", "example-key")), Environment(Environments.Production), Rules);

        act.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("Sample:ApiKey").And.Contain("(see-guide)").And.Contain("Production host refuses to start");
    }

    [Fact]
    public void EnsureReplaced_ValueCheckFails_ThrowsNamingCheckedKey()
    {
        var act = () => PlaceholderSecretGuard.EnsureReplaced(Configuration(("Sample:Connection", "bad")), Environment(Environments.Production), Rules);

        act.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("Sample:Connection");
    }

    [Fact]
    public void EnsureReplaced_RequiredKeyMissing_ThrowsNamingRequiredKey()
    {
        var act = () => PlaceholderSecretGuard.EnsureReplaced(new ConfigurationBuilder().Build(), Environment(Environments.Production), Rules);

        act.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain(RequiredKey);
    }

    [Fact]
    public void EnsureReplaced_RequiredKeyAlsoPlaceholderSecret_NamedOnce()
    {
        var rules = RulesFor(["Sample:ApiKey", RequiredKey]);

        var act = () => PlaceholderSecretGuard.EnsureReplaced(Configuration((RequiredKey, "example-x")), Environment(Environments.Production), rules);

        act.Should().Throw<InvalidOperationException>().Which.Message.Split(RequiredKey).Should().HaveCount(2);
    }

    [Fact]
    public void EnsureReplaced_PlaceholderValue_IsNeverInTheMessage()
    {
        var act = () => PlaceholderSecretGuard.EnsureReplaced(Configuration(("Sample:Token", "example-UNIQUE-4711")), Environment(Environments.Production), Rules);

        act.Should().Throw<InvalidOperationException>().Which.Message.Should().NotContain("UNIQUE-4711");
    }

    [Fact]
    public void EnsureReplaced_AllReplaced_DoesNotThrow()
    {
        var configuration = Configuration(("Sample:ApiKey", "not-a-secret-api-key"), ("Sample:Token", "not-a-secret-token"), ("Sample:Connection", "not-a-secret-connection"));

        var act = () => PlaceholderSecretGuard.EnsureReplaced(configuration, Environment(Environments.Staging), Rules);

        act.Should().NotThrow();
    }

    private static PlaceholderSecretRules RulesFor(IReadOnlyList<string> secretKeys) => new(secretKeys, x => x?.StartsWith("example-", StringComparison.Ordinal) == true, [new("Sample:Connection", x => x == "bad")], [RequiredKey], "see-guide");

    private static IConfiguration Configuration(params (string Key, string Value)[] settings)
    {
        var values = settings
            .Select(x => new KeyValuePair<string, string?>(x.Key, x.Value))
            .Prepend(new KeyValuePair<string, string?>(RequiredKey, "not-a-secret-req"))
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
