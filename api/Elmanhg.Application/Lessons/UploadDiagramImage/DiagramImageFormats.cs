using Core.Validation.Files;

namespace Elmanhg.Application.Lessons.UploadDiagramImage;

public static class DiagramImageFormats
{
    // Raster formats only: SVG can carry script and media is served from the API origin.
    public static readonly IReadOnlyList<string> Extensions = [".png", ".jpg", ".jpeg", ".webp"];
    public static readonly IReadOnlySet<string> ContentTypes = new HashSet<string>(["image/png", "image/jpeg", "image/webp"], StringComparer.OrdinalIgnoreCase);
    public static readonly IReadOnlyList<FileSignature> Signatures = [FileSignature.Png, FileSignature.Jpeg, FileSignature.WebP];
}
