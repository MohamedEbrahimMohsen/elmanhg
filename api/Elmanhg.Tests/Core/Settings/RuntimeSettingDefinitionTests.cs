using Core.DDD.Models;
using Core.Settings;
using FluentAssertions;
using System.Text.Json;

namespace Elmanhg.Tests.Core.Settings;

public sealed class RuntimeSettingDefinitionTests
{
    private static readonly LocalizedText Text = new("نص", "Text");

    [Fact]
    public void ForInteger_SetsIntegerTypeRangeAndDefault()
    {
        var definition = RuntimeSettingDefinition.ForInteger(ProbeRuntimeSettings.Count, ProbeRuntimeSettings.LimitsGroup, 5, 1, 10, Text, Text);

        (definition.Type, definition.Minimum, definition.Maximum, definition.DefaultValue.GetInt32(), definition.AllowedValues.Count).Should().Be((RuntimeSettingType.Integer, 1m, 10m, 5, 0));
    }

    [Fact]
    public void ForBoolean_HasNoRange()
    {
        var definition = RuntimeSettingDefinition.ForBoolean(ProbeRuntimeSettings.Enabled, ProbeRuntimeSettings.FlagsGroup, true, Text, Text);

        (definition.Type, definition.Minimum, definition.Maximum).Should().Be((RuntimeSettingType.Boolean, (decimal?)null, (decimal?)null));
    }

    [Fact]
    public void ForChoiceList_SetsAllowedValuesAndArrayDefault()
    {
        IReadOnlyList<string> allowed = ["Red", "Blue"];

        var definition = RuntimeSettingDefinition.ForChoiceList(new RuntimeSettingKey<List<string>>("probe.colours"), ProbeRuntimeSettings.FlagsGroup, ["Red"], allowed, Text, Text);

        definition.Type.Should().Be(RuntimeSettingType.ChoiceList);
        definition.AllowedValues.Should().Equal(allowed);
        definition.DefaultValue.ValueKind.Should().Be(JsonValueKind.Array);
    }
}
