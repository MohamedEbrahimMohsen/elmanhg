using Microsoft.AspNetCore.Http;

namespace Elmanhg.Application.TeacherThreads.CreateTeacherThread;

public static class TeacherThreadImageFormats
{
    public const string StorageFolder = "teacher-threads";

    private const int SignatureLength = 12;

    // Raster photos only: SVG can carry script and media is served from the API origin.
    public static readonly IReadOnlyList<string> Extensions = [".png", ".jpg", ".jpeg", ".webp"];
    public static readonly IReadOnlySet<string> ContentTypes = new HashSet<string>(["image/png", "image/jpeg", "image/webp"], StringComparer.OrdinalIgnoreCase);

    private static ReadOnlySpan<byte> Png => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static ReadOnlySpan<byte> Jpeg => [0xFF, 0xD8, 0xFF];

    public static bool HasMatchingSignature(IFormFile file)
    {
        Span<byte> buffer = stackalloc byte[SignatureLength];
        using var stream = file.OpenReadStream();
        var header = buffer[..stream.ReadAtLeast(buffer, SignatureLength, throwOnEndOfStream: false)];
        return Path.GetExtension(file.FileName).ToLowerInvariant() switch
        {
            ".png" => header.StartsWith(Png),
            ".jpg" or ".jpeg" => header.StartsWith(Jpeg),
            ".webp" => header.Length == SignatureLength && header[..4].SequenceEqual("RIFF"u8) && header[8..].SequenceEqual("WEBP"u8),
            _ => false,
        };
    }
}
