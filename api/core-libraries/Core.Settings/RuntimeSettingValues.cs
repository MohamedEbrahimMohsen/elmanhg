using System.Text.Json;

namespace Core.Settings;

public sealed class RuntimeSettingValues
{
    private readonly IReadOnlyDictionary<string, JsonElement> values;

    private RuntimeSettingValues(IReadOnlyDictionary<string, JsonElement> values)
    {
        this.values = values;
    }

    public static RuntimeSettingValues Defaults(RuntimeSettingRegistry registry) => From(registry, []);

    public static RuntimeSettingValues From(RuntimeSettingRegistry registry, IEnumerable<IRuntimeSettingOverride> overrides)
    {
        var stored = overrides
            .Where(x => x.Value is not null)
            .ToDictionary(x => x.Key, x => x.Value!, StringComparer.Ordinal);
        var effective = registry.Definitions.ToDictionary(x => x.Key, x => EffectiveValue(x, stored.GetValueOrDefault(x.Key)), StringComparer.Ordinal);
        return new RuntimeSettingValues(effective);
    }

    public T Get<T>(RuntimeSettingKey<T> key) => RuntimeSettingJson.Read<T>(Raw(key.Name));

    public JsonElement Raw(string key) => values.TryGetValue(key, out var value) ? value : throw new InvalidOperationException($"Runtime setting {key} is not registered.");

    public RuntimeSettingValues With(string key, JsonElement value)
    {
        var copy = new Dictionary<string, JsonElement>(values, StringComparer.Ordinal)
        {
            [key] = value,
        };
        return new RuntimeSettingValues(copy);
    }

    private static JsonElement EffectiveValue(RuntimeSettingDefinition definition, string? stored)
    {
        return stored is not null && RuntimeSettingJson.TryParse(stored, out var value) && RuntimeSettingValueRules.IsValid(definition, value)
            ? value
            : definition.DefaultValue;
    }
}
