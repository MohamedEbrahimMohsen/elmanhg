using Core.Validation.Files;
using Microsoft.AspNetCore.Http;

namespace Elmanhg.Application.TeacherThreads.CreateTeacherThread;

public static class TeacherThreadImageFormats
{
    public const string StorageFolder = "teacher-threads";

    // Raster photos only: SVG can carry script and media is served from the API origin.
    public static readonly IReadOnlyList<string> Extensions = [".png", ".jpg", ".jpeg", ".webp"];
    public static readonly IReadOnlySet<string> ContentTypes = new HashSet<string>(["image/png", "image/jpeg", "image/webp"], StringComparer.OrdinalIgnoreCase);

    public static readonly IReadOnlyList<FileSignature> Signatures = [FileSignature.Png, FileSignature.Jpeg, FileSignature.WebP];

    public static bool HasMatchingSignature(IFormFile file) => FileSignature.Matches(file, Signatures);
}
