using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Elmanhg.Application.TrainingExports.Shared;

public static class TrainingDataScrubber
{
    private const RegexOptions Options = RegexOptions.NonBacktracking | RegexOptions.CultureInvariant;
    private static readonly Regex Email = new(@"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", Options | RegexOptions.IgnoreCase);
    private static readonly Regex Url = new(@"(?:https?://|www\.)[^\s]+", Options | RegexOptions.IgnoreCase);
    private static readonly Regex Handle = new(@"@\w{3,}", Options);
    // ASCII, Arabic-Indic and Extended Arabic-Indic digits.
    private const string Digit = @"[0-9\u0660-\u0669\u06F0-\u06F9]";
    private static readonly Regex Number = new($@"\+?{Digit}(?:[ .-]?{Digit}){{7,}}", Options);
    private static readonly Regex IsoTimestamp = new(@"^[0-9]{4}-[0-9]{2}-[0-9]{2}(?:T[0-9]{2}:[0-9]{2}(?::[0-9]{2}(?:\.[0-9]+)?)?(?:Z|[+-][0-9]{2}:[0-9]{2})?)?$", Options);

    private static readonly HashSet<string> IdentifyingKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "attemptId", "sessionId", "conversationId", "threadId", "studentId", "userId", "teacherId", "messageId", "studentMessageId", "assistantMessageId", "essayGradeId",
    };

    public static string ScrubText(string text)
    {
        var scrubbed = Email.Replace(text, "[email]");
        scrubbed = Url.Replace(scrubbed, "[url]");
        scrubbed = Handle.Replace(scrubbed, "[handle]");
        return Number.Replace(scrubbed, "[number]");
    }

    public static JsonNode? ScrubJson(string? json) => json is null ? null : Scrub(JsonNode.Parse(json), null);

    private static JsonNode? Scrub(JsonNode? node, string? key)
    {
        return node switch
        {
            JsonObject item => new JsonObject(item
                .Where(x => !IdentifyingKeys.Contains(x.Key))
                .Select(x => KeyValuePair.Create(x.Key, Scrub(x.Value, x.Key)))),
            JsonArray items => new JsonArray(items
                .Select(x => Scrub(x, key))
                .ToArray()),
            JsonValue value when value.GetValueKind() == JsonValueKind.String => JsonValue.Create(ScrubString(value.GetValue<string>(), key)),
            _ => node?.DeepClone(),
        };
    }

    // Whole-value GUIDs (content and option ids) and real timestamps under structural `*At` keys are not contact data, and their digit runs would otherwise be masked.
    private static string ScrubString(string value, string? key) => Guid.TryParseExact(value, "D", out _) || (IsTimestampKey(key) && IsTimestamp(value)) ? value : ScrubText(value);

    private static bool IsTimestampKey(string? key) => key is { Length: > 2 } && key.EndsWith("At", StringComparison.Ordinal);

    private static bool IsTimestamp(string value) => IsoTimestamp.IsMatch(value) && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _);
}
