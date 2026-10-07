using Microsoft.AspNetCore.Http;

namespace Core.Validation.Files;

public sealed class FileSignature
{
    private FileSignature(IReadOnlyList<string> extensions, IReadOnlyList<byte?[]> patterns)
    {
        Extensions = extensions;
        Patterns = patterns;
    }

    public IReadOnlyList<string> Extensions { get; }
    public IReadOnlyList<byte?[]> Patterns { get; }

    public static FileSignature Create(IReadOnlyList<string> extensions, params byte?[][] patterns)
    {
        ArgumentOutOfRangeException.ThrowIfZero(patterns.Length, nameof(patterns));
        return new([.. extensions.Select(extension => extension.ToLowerInvariant())], patterns);
    }

    public static readonly FileSignature Png = Create([".png"], [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
    public static readonly FileSignature Jpeg = Create([".jpg", ".jpeg"], [0xFF, 0xD8, 0xFF]);
    public static readonly FileSignature WebP = Create([".webp"], [0x52, 0x49, 0x46, 0x46, null, null, null, null, 0x57, 0x45, 0x42, 0x50]);
    public static readonly FileSignature Gif = Create([".gif"], [0x47, 0x49, 0x46, 0x38, 0x37, 0x61], [0x47, 0x49, 0x46, 0x38, 0x39, 0x61]);
    public static readonly FileSignature WebM = Create([".webm"], [0x1A, 0x45, 0xDF, 0xA3]);
    public static readonly FileSignature Ogg = Create([".ogg"], [0x4F, 0x67, 0x67, 0x53]);
    public static readonly FileSignature Mp4 = Create([".mp4", ".m4a"], [null, null, null, null, 0x66, 0x74, 0x79, 0x70]);
    public static readonly FileSignature Zip = Create([".zip"], [0x50, 0x4B, 0x03, 0x04]);

    public FileSignature ForExtensions(params string[] extensions) => Create(extensions, [.. Patterns]);

    public bool Matches(ReadOnlySpan<byte> header)
    {
        foreach (var pattern in Patterns)
        {
            if (PatternMatches(pattern, header))
            {
                return true;
            }
        }

        return false;
    }

    public static bool Matches(IFormFile file, IReadOnlyCollection<FileSignature> signatures)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var candidates = signatures.Where(signature => signature.Extensions.Contains(extension)).ToList();
        if (candidates.Count == 0)
        {
            return false;
        }

        var length = candidates.SelectMany(signature => signature.Patterns).Max(pattern => pattern.Length);
        Span<byte> buffer = stackalloc byte[length];
        using var stream = file.OpenReadStream();
        ReadOnlySpan<byte> header = buffer[..stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false)];
        foreach (var candidate in candidates)
        {
            if (candidate.Matches(header))
            {
                return true;
            }
        }

        return false;
    }

    private static bool PatternMatches(byte?[] pattern, ReadOnlySpan<byte> header)
    {
        if (header.Length < pattern.Length)
        {
            return false;
        }

        for (var index = 0; index < pattern.Length; index++)
        {
            if (pattern[index] is { } expected && header[index] != expected)
            {
                return false;
            }
        }

        return true;
    }
}
