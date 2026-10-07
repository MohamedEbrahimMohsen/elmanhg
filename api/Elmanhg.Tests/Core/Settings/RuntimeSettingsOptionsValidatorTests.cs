using Core.Settings;
using FluentAssertions;

namespace Elmanhg.Tests.Core.Settings;

public sealed class RuntimeSettingsOptionsValidatorTests
{
    [Fact]
    public void Validate_ValidDefinitions_Succeeds()
    {
        var result = Validator(new ProbeRuntimeSettings()).Validate(null, new RuntimeSettingsOptions());

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_DefaultOutsideRange_FailsNamingKey()
    {
        var result = Validator(new ProbeRuntimeSettings(countDefault: 11)).Validate(null, new RuntimeSettingsOptions());

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("probe.count");
    }

    [Fact]
    public void Validate_GroupNotInOrder_FailsNamingGroup()
    {
        var result = Validator(new ProbeRuntimeSettings(countGroup: "Other")).Validate(null, new RuntimeSettingsOptions());

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("group Other");
    }

    private static RuntimeSettingsOptionsValidator Validator(ProbeRuntimeSettings definitions) => new([definitions], ProbeRuntimeSettings.GroupOrder);
}
