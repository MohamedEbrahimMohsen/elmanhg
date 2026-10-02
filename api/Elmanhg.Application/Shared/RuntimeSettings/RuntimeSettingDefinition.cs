using Core.DDD.Models;
using System.Text.Json;

namespace Elmanhg.Application.Shared.RuntimeSettings;

public sealed record RuntimeSettingDefinition(string Key, RuntimeSettingGroup Group, RuntimeSettingType Type, JsonElement DefaultValue, decimal? Minimum, decimal? Maximum, IReadOnlyList<string> AllowedValues, LocalizedText Label, LocalizedText Description)
{
    public static RuntimeSettingDefinition ForInteger(RuntimeSettingKey<int> key, RuntimeSettingGroup group, int defaultValue, int minimum, int maximum, LocalizedText label, LocalizedText description)
    {
        return new(key.Name, group, RuntimeSettingType.Integer, RuntimeSettingJson.ToElement(defaultValue), minimum, maximum, [], label, description);
    }

    public static RuntimeSettingDefinition ForDecimal(RuntimeSettingKey<decimal> key, RuntimeSettingGroup group, decimal defaultValue, decimal minimum, decimal maximum, LocalizedText label, LocalizedText description)
    {
        return new(key.Name, group, RuntimeSettingType.Decimal, RuntimeSettingJson.ToElement(defaultValue), minimum, maximum, [], label, description);
    }

    public static RuntimeSettingDefinition ForBoolean(RuntimeSettingKey<bool> key, RuntimeSettingGroup group, bool defaultValue, LocalizedText label, LocalizedText description)
    {
        return new(key.Name, group, RuntimeSettingType.Boolean, RuntimeSettingJson.ToElement(defaultValue), null, null, [], label, description);
    }

    public static RuntimeSettingDefinition ForChoice(RuntimeSettingKey<string> key, RuntimeSettingGroup group, string defaultValue, IReadOnlyList<string> allowedValues, LocalizedText label, LocalizedText description)
    {
        return new(key.Name, group, RuntimeSettingType.Choice, RuntimeSettingJson.ToElement(defaultValue), null, null, allowedValues, label, description);
    }

    public static RuntimeSettingDefinition ForChoiceList(RuntimeSettingKey<List<string>> key, RuntimeSettingGroup group, List<string> defaultValue, IReadOnlyList<string> allowedValues, LocalizedText label, LocalizedText description)
    {
        return new(key.Name, group, RuntimeSettingType.ChoiceList, RuntimeSettingJson.ToElement(defaultValue), null, null, allowedValues, label, description);
    }
}
