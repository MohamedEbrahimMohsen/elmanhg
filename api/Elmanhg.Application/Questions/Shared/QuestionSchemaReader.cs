using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Questions.Schemas;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Elmanhg.Application.Questions.Shared;

public static class QuestionSchemaReader
{
    // Option and blank ids are referenced from grading specs and [[id]] stem placeholders; a narrow ASCII format keeps them stable and safe.
    private static readonly Regex IdPattern = new("^[a-z0-9-]{1,20}$", RegexOptions.NonBacktracking);

    public static bool TryRead<T>(JsonElement element, [NotNullWhen(true)] out T? value) where T : class
    {
        value = null;
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        try
        {
            value = element.Deserialize<T>(QuestionJson.SerializerOptions);
        }
        catch (JsonException)
        {
            return false;
        }

        return value is not null;
    }

    public static T Read<T>(JsonElement element) where T : class
    {
        return element.Deserialize<T>(QuestionJson.SerializerOptions) ?? throw new InvalidOperationException("Question schema was not validated.");
    }

    public static bool IsValidId(string? id)
    {
        return id is not null && IdPattern.IsMatch(id);
    }

    public static bool AreValidAcceptedAnswers(List<string>? answers, ContentOptions options)
    {
        return answers is { Count: > 0 }
            && answers.Count <= options.QuestionAcceptedAnswersMaxCount
            && answers.All(x => !string.IsNullOrWhiteSpace(x) && x.Trim().Length <= options.QuestionAnswerMaxLength);
    }

    public static List<string> TrimAnswers(List<string>? answers)
    {
        return (answers ?? [])
            .Select(x => x.Trim())
            .ToList();
    }

    public static void AddIf(List<string> errors, bool condition, string code)
    {
        if (condition)
        {
            errors.Add(code);
        }
    }

    public static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, QuestionJson.SerializerOptions);
    }
}
