using Microsoft.AspNetCore.Http;

namespace Elmanhg.Application.TeacherInbox.RecordVoiceDraft;

public static class TeacherVoiceFormats
{
    private const int SignatureLength = 12;

    public static readonly IReadOnlyList<string> Extensions = [".webm", ".ogg", ".m4a", ".mp4"];
    public static readonly IReadOnlySet<string> MediaTypes = new HashSet<string>(["audio/webm", "audio/ogg", "audio/mp4"], StringComparer.OrdinalIgnoreCase);

    private static ReadOnlySpan<byte> Webm => [0x1A, 0x45, 0xDF, 0xA3];

    public static bool HasAllowedMediaType(IFormFile file) => MediaTypes.Contains(file.ContentType.Split(';')[0].Trim());

    public static bool HasMatchingSignature(IFormFile file)
    {
        Span<byte> buffer = stackalloc byte[SignatureLength];
        using var stream = file.OpenReadStream();
        var header = buffer[..stream.ReadAtLeast(buffer, SignatureLength, throwOnEndOfStream: false)];
        return Path.GetExtension(file.FileName).ToLowerInvariant() switch
        {
            ".webm" => header.StartsWith(Webm),
            ".ogg" => header.StartsWith("OggS"u8),
            ".m4a" or ".mp4" => header.Length >= 8 && header[4..8].SequenceEqual("ftyp"u8),
            _ => false,
        };
    }
}
