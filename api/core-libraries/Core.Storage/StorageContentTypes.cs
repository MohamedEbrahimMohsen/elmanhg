using Microsoft.AspNetCore.StaticFiles;

namespace Core.Storage;

public static class StorageContentTypes
{
    public const string Fallback = "application/octet-stream";

    // Only extensions whose framework type is right for stored media; anything else (e.g. .svg) must not get an inline-renderable type.
    private static readonly string[] FrameworkMappedExtensions = [".png", ".jpg", ".jpeg", ".webp", ".gif", ".m4a"];

    // The framework maps these containers to video/*; stored recordings in them are audio-only.
    private static readonly Dictionary<string, string> Overrides = new(StringComparer.OrdinalIgnoreCase)
    {
        [".webm"] = "audio/webm",
        [".ogg"] = "audio/ogg",
        [".mp4"] = "audio/mp4",
        [".jsonl"] = "application/x-ndjson",
    };

    private static readonly FileExtensionContentTypeProvider Provider = Create();

    public static string FromKey(string key) => Provider.TryGetContentType(key, out var contentType) ? contentType : Fallback;

    private static FileExtensionContentTypeProvider Create()
    {
        var defaults = new FileExtensionContentTypeProvider().Mappings;
        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var extension in FrameworkMappedExtensions)
        {
            mappings[extension] = defaults[extension];
        }

        foreach (var (extension, contentType) in Overrides)
        {
            mappings[extension] = contentType;
        }

        return new FileExtensionContentTypeProvider(mappings);
    }
}
