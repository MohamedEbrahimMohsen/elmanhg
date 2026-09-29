using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Elmanhg.Tests.Integration.Observability;

public sealed class TelemetryRegistrationTests(ApiFactory factory)
{
    [Fact]
    public void Services_Default_RegisterTracerAndMeterProviders()
    {
        factory.Services.GetService<TracerProvider>().Should().NotBeNull();
        factory.Services.GetService<MeterProvider>().Should().NotBeNull();
    }

    [Fact]
    public async Task Start_NonHttpOtlpEndpoint_FailsOptionsValidation()
    {
        await using var misconfigured = factory.WithWebHostBuilder(builder => builder.UseSetting("Observability:OtlpEndpoint", "ftp://collector"));

        var act = () => misconfigured.CreateClient();

        act.Should().Throw<OptionsValidationException>().WithMessage("*Observability:OtlpEndpoint*");
    }
}
