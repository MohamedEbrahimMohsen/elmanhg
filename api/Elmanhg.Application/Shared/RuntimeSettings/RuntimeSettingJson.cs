using System.Text.Json;

namespace Elmanhg.Application.Shared.RuntimeSettings;

public static class RuntimeSettingJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static JsonElement ToElement<T>(T value) => JsonSerializer.SerializeToElement(value, Options);

    public static T Read<T>(JsonElement element)
    {
        return element.Deserialize<T>(Options) ?? throw new InvalidOperationException("Runtime setting value is null.");
    }

    public static bool TryParse(string json, out JsonElement element)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            element = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            element = default;
            return false;
        }
    }
}
