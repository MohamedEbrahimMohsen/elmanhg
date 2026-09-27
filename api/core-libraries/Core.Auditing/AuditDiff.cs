using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core.Auditing;

public static class AuditDiff
{
    public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string? Serialize(IReadOnlyList<AuditEntityChange> changes) => changes.Count == 0 ? null : JsonSerializer.Serialize(changes, SerializerOptions);
}
