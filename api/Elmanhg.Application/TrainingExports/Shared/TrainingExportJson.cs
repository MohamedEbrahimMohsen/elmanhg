using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Elmanhg.Application.TrainingExports.Shared;

public static class TrainingExportJson
{
    // Relaxed escaping keeps Arabic readable; the file is only ever served as an application/x-ndjson attachment with nosniff, never as HTML.
    public static JsonSerializerOptions SerializerOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
