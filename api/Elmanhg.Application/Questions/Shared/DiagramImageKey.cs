using System.Text.RegularExpressions;

namespace Elmanhg.Application.Questions.Shared;

public static class DiagramImageKey
{
    public const string StorageFolder = "question-diagrams";

    // Only the keys the diagram upload writes: folder + lesson id + 32-hex name + raster extension. A key is never a URL, so no host can be referenced.
    private static readonly Regex Pattern = new(@"^question-diagrams/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/[0-9a-f]{32}\.(png|jpg|jpeg|webp)$", RegexOptions.NonBacktracking);

    public static bool IsValid(string? key) => key is not null && Pattern.IsMatch(key);

    public static bool BelongsToLesson(string? key, Guid lessonId) => IsValid(key) && key!.StartsWith($"{StorageFolder}/{lessonId}/", StringComparison.Ordinal);
}
