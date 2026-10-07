using System.Text.Json;

namespace Core.Settings;

public static class RuntimeSettingValueRules
{
    public static bool IsValid(RuntimeSettingDefinition definition, JsonElement value)
    {
        return definition.Type switch
        {
            RuntimeSettingType.Integer => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && number >= definition.Minimum && number <= definition.Maximum,
            RuntimeSettingType.Decimal => value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number) && number >= definition.Minimum && number <= definition.Maximum,
            RuntimeSettingType.Boolean => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            RuntimeSettingType.Choice => value.ValueKind == JsonValueKind.String && IsAllowed(definition, value),
            RuntimeSettingType.ChoiceList => value.ValueKind == JsonValueKind.Array && IsAllowedDistinctList(definition, value),
            _ => false,
        };
    }

    private static bool IsAllowed(RuntimeSettingDefinition definition, JsonElement value) => definition.AllowedValues.Contains(value.GetString(), StringComparer.Ordinal);

    private static bool IsAllowedDistinctList(RuntimeSettingDefinition definition, JsonElement value)
    {
        var items = value.EnumerateArray().ToList();
        return items.All(x => x.ValueKind == JsonValueKind.String && IsAllowed(definition, x))
            && items.Select(x => x.GetString()).Distinct(StringComparer.Ordinal).Count() == items.Count;
    }
}
