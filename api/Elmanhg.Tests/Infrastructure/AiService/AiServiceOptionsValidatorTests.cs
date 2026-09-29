using Elmanhg.Infrastructure.AiService;
using FluentAssertions;

namespace Elmanhg.Tests.Infrastructure.AiService;

public sealed class AiServiceOptionsValidatorTests
{
    private readonly AiServiceOptionsValidator _validator = new();

    [Fact]
    public void Validate_FakeProviderWithoutToken_Succeeds()
    {
        var result = _validator.Validate(null, AiServiceTestSettings.Fake());

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_CompleteHttpSettings_Succeeds()
    {
        var result = _validator.Validate(null, AiServiceTestSettings.WithHttp());

        result.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("elmanhg-tests-ai-service-token-")]
    public void Validate_HttpWithShortToken_FailsNamingServiceToken(string token)
    {
        var options = AiServiceTestSettings.WithHttp();
        options.ServiceToken = token;

        var result = _validator.Validate(null, options);

        result.Failures.Should().Contain(x => x.Contains("AiService:ServiceToken", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("localhost:8000")]
    [InlineData("/v1")]
    [InlineData("ftp://ai.test")]
    public void Validate_HttpWithInvalidBaseUrl_FailsNamingBaseUrl(string baseUrl)
    {
        var options = AiServiceTestSettings.WithHttp();
        options.BaseUrl = baseUrl;

        var result = _validator.Validate(null, options);

        result.Failures.Should().Contain(x => x.Contains("AiService:BaseUrl", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_AttemptTimeoutAboveTotal_Fails()
    {
        var options = AiServiceTestSettings.Fake();
        options.AttemptTimeoutSeconds = 60;
        options.TotalTimeoutSeconds = 50;

        var result = _validator.Validate(null, options);

        result.Failures.Should().Contain(x => x.Contains("AttemptTimeoutSeconds", StringComparison.Ordinal));
    }
}
