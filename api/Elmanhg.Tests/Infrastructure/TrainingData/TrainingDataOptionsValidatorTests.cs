using Elmanhg.Infrastructure.TrainingData;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Elmanhg.Tests.Infrastructure.TrainingData;

public sealed class TrainingDataOptionsValidatorTests
{
    private const string ValidKey = "0123456789abcdef0123456789abcdef";

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Demo")]
    public void Validate_EmptyKeyInRequiredEnvironment_Fails(string environment)
    {
        var result = Validate(environment, string.Empty);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("required");
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Demo")]
    public void Validate_DevelopmentKeyInProduction_Fails(string environment)
    {
        var result = Validate(environment, TrainingDataOptions.DevelopmentStudentIdHashKey);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("development key");
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void Validate_ShortKey_FailsInAnyEnvironment(string environment)
    {
        var result = Validate(environment, ValidKey[..31]);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("at least 32 characters");
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void Validate_EmptyKeyInDevelopmentOrTesting_Succeeds(string environment)
    {
        var result = Validate(environment, string.Empty);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_32CharacterKeyInProduction_Succeeds()
    {
        var result = Validate("Production", ValidKey);

        result.Succeeded.Should().BeTrue();
    }

    private static ValidateOptionsResult Validate(string environment, string key)
    {
        var hostEnvironment = Substitute.For<IHostEnvironment>();
        hostEnvironment.EnvironmentName.Returns(environment);
        return new TrainingDataOptionsValidator(hostEnvironment).Validate(null, new TrainingDataOptions { StudentIdHashKey = key });
    }
}
