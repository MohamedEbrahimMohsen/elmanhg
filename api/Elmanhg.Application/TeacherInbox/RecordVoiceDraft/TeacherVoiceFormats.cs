using Core.Validation.Files;
using Microsoft.AspNetCore.Http;

namespace Elmanhg.Application.TeacherInbox.RecordVoiceDraft;

public static class TeacherVoiceFormats
{
    public static readonly IReadOnlyList<string> Extensions = [".webm", ".ogg", ".m4a", ".mp4"];
    public static readonly IReadOnlySet<string> MediaTypes = new HashSet<string>(["audio/webm", "audio/ogg", "audio/mp4"], StringComparer.OrdinalIgnoreCase);

    public static readonly IReadOnlyList<FileSignature> Signatures = [FileSignature.WebM, FileSignature.Ogg, FileSignature.Mp4];

    public static bool HasAllowedMediaType(IFormFile file) => MediaTypes.Contains(file.ContentType.Split(';')[0].Trim());

    public static bool HasMatchingSignature(IFormFile file) => FileSignature.Matches(file, Signatures);
}
