using Core.Settings;
using FluentAssertions;

namespace Elmanhg.Tests.Core.Settings;

public sealed class RuntimeSettingValuesTests
{
    private readonly RuntimeSettingRegistry _registry = new([new ProbeRuntimeSettings()], ProbeRuntimeSettings.GroupOrder);

    [Fact]
    public void Defaults_NoOverrides_ReturnsDefinitionDefaults()
    {
        var values = RuntimeSettingValues.Defaults(_registry);

        (values.Get(ProbeRuntimeSettings.Count), values.Get(ProbeRuntimeSettings.Enabled)).Should().Be((5, false));
    }

    [Fact]
    public void From_ValidOverride_ReturnsOverride()
    {
        var values = RuntimeSettingValues.From(_registry, [new ProbeOverride("probe.count", "7")]);

        values.Get(ProbeRuntimeSettings.Count).Should().Be(7);
    }

    [Fact]
    public void From_OverrideOutsideRange_FallsBackToDefault()
    {
        var values = RuntimeSettingValues.From(_registry, [new ProbeOverride("probe.count", "50")]);

        values.Get(ProbeRuntimeSettings.Count).Should().Be(5);
    }

    [Fact]
    public void From_OverrideNotJson_FallsBackToDefault()
    {
        var values = RuntimeSettingValues.From(_registry, [new ProbeOverride("probe.count", "not-json")]);

        values.Get(ProbeRuntimeSettings.Count).Should().Be(5);
    }

    [Fact]
    public void From_NullValueOrUnknownKey_IsIgnored()
    {
        var values = RuntimeSettingValues.From(_registry, [new ProbeOverride("probe.count", null), new ProbeOverride("probe.unknown", "1")]);

        var act = () => values.Raw("probe.unknown");

        values.Get(ProbeRuntimeSettings.Count).Should().Be(5);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void With_ReplacesOnlyThatKey()
    {
        var original = RuntimeSettingValues.Defaults(_registry);

        var changed = original.With("probe.count", RuntimeSettingJson.ToElement(7));

        (changed.Get(ProbeRuntimeSettings.Count), changed.Get(ProbeRuntimeSettings.Ceiling), original.Get(ProbeRuntimeSettings.Count)).Should().Be((7, 8, 5));
    }

    [Fact]
    public void Raw_UnregisteredKey_Throws()
    {
        var values = RuntimeSettingValues.Defaults(_registry);

        var act = () => values.Raw("probe.unknown");

        act.Should().Throw<InvalidOperationException>();
    }
}
