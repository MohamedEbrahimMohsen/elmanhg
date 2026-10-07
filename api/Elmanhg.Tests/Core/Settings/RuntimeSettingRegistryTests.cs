using Core.Errors;
using Core.Settings;
using FluentAssertions;

namespace Elmanhg.Tests.Core.Settings;

public sealed class RuntimeSettingRegistryTests
{
    private readonly RuntimeSettingRegistry _registry = new([new ProbeRuntimeSettings()], ProbeRuntimeSettings.GroupOrder);

    [Fact]
    public void Constructor_ValidDefinitions_OrdersByGroupOrderKeepingDeclarationOrder()
    {
        _registry.Definitions.Select(x => x.Key).Should().Equal("probe.count", "probe.ceiling", "probe.enabled");
    }

    [Fact]
    public void Constructor_DuplicateKey_ThrowsNamingKey()
    {
        var probe = new ProbeRuntimeSettings();

        var act = () => new RuntimeSettingRegistry([probe, probe], ProbeRuntimeSettings.GroupOrder);

        act.Should().Throw<InvalidOperationException>().WithMessage("*probe.count*registered more than once*");
    }

    [Fact]
    public void Constructor_GroupNotInOrder_ThrowsNamingKeyAndGroup()
    {
        var act = () => new RuntimeSettingRegistry([new ProbeRuntimeSettings(countGroup: "Other")], ProbeRuntimeSettings.GroupOrder);

        act.Should().Throw<InvalidOperationException>().WithMessage("*probe.count*group Other*");
    }

    [Fact]
    public void FindProblems_DefaultOutsideRange_ReportsKey()
    {
        var problems = RuntimeSettingRegistry.FindProblems([new ProbeRuntimeSettings(countDefault: 11)], ProbeRuntimeSettings.GroupOrder);

        problems.Should().ContainSingle().Which.Should().Contain("probe.count");
    }

    [Fact]
    public void FindProblems_ValidDefinitions_ReturnsEmpty()
    {
        var problems = RuntimeSettingRegistry.FindProblems([new ProbeRuntimeSettings()], ProbeRuntimeSettings.GroupOrder);

        problems.Should().BeEmpty();
    }

    [Fact]
    public void Find_KnownKey_ReturnsDefinition()
    {
        var definition = _registry.Find("probe.count");

        (definition!.Type, definition.Group, definition.Minimum, definition.Maximum).Should().Be((RuntimeSettingType.Integer, "Limits", 1m, 10m));
    }

    [Fact]
    public void Find_UnknownKey_ReturnsNull() => _registry.Find("probe.unknown").Should().BeNull();

    [Fact]
    public void EnsureConstraintsHold_Satisfied_DoesNotThrow()
    {
        var act = () => _registry.EnsureConstraintsHold(RuntimeSettingValues.Defaults(_registry));

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureConstraintsHold_Broken_ThrowsWithConstraintCode()
    {
        var broken = RuntimeSettingValues.Defaults(_registry).With("probe.count", RuntimeSettingJson.ToElement(9));

        var act = () => _registry.EnsureConstraintsHold(broken);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ProbeRuntimeSettings.CountAboveCeilingCode);
    }
}
