namespace Elmanhg.Application.Shared.Storage;

public static class MediaContentTypes
{
    public const string Fallback = "application/octet-stream";

    public static string FromKey(string key)
    {
        return Path.GetExtension(key).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".webm" => "audio/webm",
            ".ogg" => "audio/ogg",
            ".m4a" or ".mp4" => "audio/mp4",
            ".jsonl" => "application/x-ndjson",
            _ => Fallback,
        };
    }
}
