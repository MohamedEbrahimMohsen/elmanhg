using Core.Observability;
using FluentAssertions;

namespace Elmanhg.Tests.Core.Observability;

public sealed class ObservabilityOptionsValidatorTests
{
    private readonly ObservabilityOptionsValidator _validator = new();

    [Fact]
    public void Validate_Defaults_Succeeds()
    {
        _validator.Validate(null, new ObservabilityOptions()).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_HttpEndpointWithHeaders_Succeeds()
    {
        var options = new ObservabilityOptions { OtlpEndpoint = "http://otel-collector:4317", OtlpHeaders = "authorization=Bearer x" };

        _validator.Validate(null, options).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_RelativeEndpoint_Fails()
    {
        var result = _validator.Validate(null, new ObservabilityOptions { OtlpEndpoint = "collector/v1/traces" });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Observability:OtlpEndpoint");
    }

    [Fact]
    public void Validate_NonHttpScheme_Fails()
    {
        _validator.Validate(null, new ObservabilityOptions { OtlpEndpoint = "ftp://collector" }).Failed.Should().BeTrue();
    }

    [Fact]
    public void Validate_HeaderWithoutEquals_Fails()
    {
        const string header = "BearerSecretToken";

        var result = _validator.Validate(null, new ObservabilityOptions { OtlpHeaders = header });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().NotContain(header);
    }

    [Fact]
    public void Validate_HeaderWithEmptyKey_Fails()
    {
        _validator.Validate(null, new ObservabilityOptions { OtlpHeaders = "=value" }).Failed.Should().BeTrue();
    }
}
