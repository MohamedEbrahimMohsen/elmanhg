using Core.Utilities;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

namespace Elmanhg.Tests.Core.Utilities;

public sealed class ValidatedOptionsServiceCollectionExtensionsTests
{
    [Fact]
    public void AddValidatedOptions_BoundSection_BindsValues()
    {
        using var provider = Services("5").AddValidatedOptions<ProbeOptions>(ProbeOptions.SectionName).Services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<ProbeOptions>>().Value.Limit.Should().Be(5);
    }

    [Fact]
    public void AddValidatedOptions_DataAnnotationViolation_FailsStartupValidation()
    {
        using var provider = Services("50").AddValidatedOptions<ProbeOptions>(ProbeOptions.SectionName).Services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<IStartupValidator>().Validate();

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void AddValidatedOptions_WithValidator_RunsValidatorAtStartup()
    {
        using var provider = Services("5").AddValidatedOptions<ProbeOptions, FailingProbeValidator>(ProbeOptions.SectionName).Services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<IStartupValidator>().Validate();

        act.Should().Throw<OptionsValidationException>().Which.Failures.Should().Contain("x");
    }

    [Fact]
    public void AddValidatedOptions_WithValidator_RegistersSingletonValidator()
    {
        var services = Services("5");

        services.AddValidatedOptions<ProbeOptions, FailingProbeValidator>(ProbeOptions.SectionName);

        var descriptor = services.Should().ContainSingle(x => x.ServiceType == typeof(IValidateOptions<ProbeOptions>) && x.ImplementationType == typeof(FailingProbeValidator)).Which;
        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    private static ServiceCollection Services(string limit)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [$"{ProbeOptions.SectionName}:Limit"] = limit }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        return services;
    }

    private sealed class ProbeOptions
    {
        public const string SectionName = "Probe";

        [Range(1, 10)]
        public int Limit { get; set; }
    }

    private sealed class FailingProbeValidator : IValidateOptions<ProbeOptions>
    {
        public ValidateOptionsResult Validate(string? name, ProbeOptions options) => ValidateOptionsResult.Fail("x");
    }
}
