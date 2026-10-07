using Core.DDD.Models;
using Core.Settings;
using FluentAssertions;
using System.Text.Json;

namespace Elmanhg.Tests.Core.Settings;

public sealed class RuntimeSettingValueRulesTests
{
    private static readonly LocalizedText Text = new("نص", "Text");
    private static readonly RuntimeSettingDefinition Integer = RuntimeSettingDefinition.ForInteger(new RuntimeSettingKey<int>("test.integer"), "Probe", 24, 1, 168, Text, Text);
    private static readonly RuntimeSettingDefinition Decimal = RuntimeSettingDefinition.ForDecimal(new RuntimeSettingKey<decimal>("test.decimal"), "Probe", 0.7m, 0m, 1m, Text, Text);
    private static readonly RuntimeSettingDefinition Boolean = RuntimeSettingDefinition.ForBoolean(new RuntimeSettingKey<bool>("test.boolean"), "Probe", false, Text, Text);
    private static readonly RuntimeSettingDefinition Choice = RuntimeSettingDefinition.ForChoice(new RuntimeSettingKey<string>("test.choice"), "Probe", "WhatsApp", ["WhatsApp", "Email"], Text, Text);
    private static readonly RuntimeSettingDefinition ChoiceList = RuntimeSettingDefinition.ForChoiceList(new RuntimeSettingKey<List<string>>("test.choiceList"), "Probe", ["WhatsApp"], ["WhatsApp", "Email"], Text, Text);

    [Fact]
    public void IsValid_IntegerInRange_ReturnsTrue() => RuntimeSettingValueRules.IsValid(Integer, Json("12")).Should().BeTrue();

    [Theory]
    [InlineData("0")]
    [InlineData("169")]
    public void IsValid_IntegerOutOfRange_ReturnsFalse(string json) => RuntimeSettingValueRules.IsValid(Integer, Json(json)).Should().BeFalse();

    [Fact]
    public void IsValid_IntegerWithFraction_ReturnsFalse() => RuntimeSettingValueRules.IsValid(Integer, Json("12.5")).Should().BeFalse();

    [Fact]
    public void IsValid_IntegerAsString_ReturnsFalse() => RuntimeSettingValueRules.IsValid(Integer, Json("\"12\"")).Should().BeFalse();

    [Fact]
    public void IsValid_DecimalInRange_ReturnsTrue() => RuntimeSettingValueRules.IsValid(Decimal, Json("0.75")).Should().BeTrue();

    [Fact]
    public void IsValid_DecimalAboveMax_ReturnsFalse() => RuntimeSettingValueRules.IsValid(Decimal, Json("1.2")).Should().BeFalse();

    [Fact]
    public void IsValid_BooleanValue_ReturnsTrue() => RuntimeSettingValueRules.IsValid(Boolean, Json("true")).Should().BeTrue();

    [Fact]
    public void IsValid_BooleanAsNumber_ReturnsFalse() => RuntimeSettingValueRules.IsValid(Boolean, Json("1")).Should().BeFalse();

    [Fact]
    public void IsValid_ChoiceAllowed_ReturnsTrue() => RuntimeSettingValueRules.IsValid(Choice, Json("\"Email\"")).Should().BeTrue();

    [Fact]
    public void IsValid_ChoiceNotAllowed_ReturnsFalse() => RuntimeSettingValueRules.IsValid(Choice, Json("\"Sms\"")).Should().BeFalse();

    [Fact]
    public void IsValid_ChoiceListDistinctAllowed_ReturnsTrue() => RuntimeSettingValueRules.IsValid(ChoiceList, Json("[\"WhatsApp\",\"Email\"]")).Should().BeTrue();

    [Fact]
    public void IsValid_ChoiceListDuplicate_ReturnsFalse() => RuntimeSettingValueRules.IsValid(ChoiceList, Json("[\"Email\",\"Email\"]")).Should().BeFalse();

    [Fact]
    public void IsValid_ChoiceListUnknownItem_ReturnsFalse() => RuntimeSettingValueRules.IsValid(ChoiceList, Json("[\"Email\",\"Sms\"]")).Should().BeFalse();

    [Fact]
    public void IsValid_UndefinedOrNull_ReturnsFalse()
    {
        (RuntimeSettingValueRules.IsValid(Integer, default), RuntimeSettingValueRules.IsValid(Integer, Json("null"))).Should().Be((false, false));
    }

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
