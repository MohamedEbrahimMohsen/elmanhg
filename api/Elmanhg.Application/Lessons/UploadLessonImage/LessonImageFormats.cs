using Core.Validation.Files;
using Microsoft.AspNetCore.Http;

namespace Elmanhg.Application.Lessons.UploadLessonImage;

public static class LessonImageFormats
{
    // Raster formats only: SVG can carry script and media is served from the API origin.
    public static readonly IReadOnlyList<string> Extensions = [".png", ".jpg", ".jpeg", ".webp", ".gif"];
    public static readonly IReadOnlySet<string> ContentTypes = new HashSet<string>(["image/png", "image/jpeg", "image/webp", "image/gif"], StringComparer.OrdinalIgnoreCase);

    public static readonly IReadOnlyList<FileSignature> Signatures = [FileSignature.Png, FileSignature.Jpeg, FileSignature.WebP, FileSignature.Gif];

    public static bool HasMatchingSignature(IFormFile file) => FileSignature.Matches(file, Signatures);
}
