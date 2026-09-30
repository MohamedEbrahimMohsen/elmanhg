using Elmanhg.Application.Shared.RichText;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Schemas;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Elmanhg.Application.Avatar.Shared;

public static partial class AvatarAnswerText
{
    // Text shown to the model, matching the UI wording for true/false answers and option lists.
    private const string TrueText = "صح";
    private const string FalseText = "خطأ";
    private const string ListSeparator = "، ";
    private const string StepSeparator = "\n";
    private const string FinalAnswerLabel = "الإجابة النهائية: ";

    public static string? StudentAnswer(QuestionRevisionSnapshot snapshot, string? answerJson, IRichTextExtractor extractor)
    {
        if (string.IsNullOrWhiteSpace(answerJson))
        {
            return null;
        }

        return snapshot.Type switch
        {
            QuestionType.Mcq => OptionText(Read<ChoiceBody>(snapshot.Body), Read<McqAnswer>(answerJson)?.OptionId, extractor),
            QuestionType.Multi => OptionTexts(Read<ChoiceBody>(snapshot.Body), Read<MultiAnswer>(answerJson)?.OptionIds, extractor),
            QuestionType.TrueFalse => BooleanText(Read<TrueFalseAnswer>(answerJson)?.Value),
            QuestionType.Fill => Blanks(Read<FillAnswer>(answerJson)?.Blanks?.Select(x => (x.Id, x.Text))),
            QuestionType.Short => NullIfBlank(Read<ShortAnswer>(answerJson)?.Text),
            QuestionType.MathSteps => MathStepsText(Read<MathStepsAnswer>(answerJson)),
            _ => null,
        };
    }

    private static string? OptionText(ChoiceBody? body, string? id, IRichTextExtractor extractor)
    {
        var option = body?.Options?.FirstOrDefault(x => x.Id is not null && x.Id == id);
        return option is null ? null : NullIfBlank(AvatarText.Plain(option.Text, extractor, int.MaxValue));
    }

    private static string? OptionTexts(ChoiceBody? body, IReadOnlyCollection<string>? ids, IRichTextExtractor extractor)
    {
        if (body?.Options is null || ids is null)
        {
            return null;
        }

        var texts = body.Options
            .Where(x => x.Id is not null && ids.Contains(x.Id))
            .Select(x => AvatarText.Plain(x.Text, extractor, int.MaxValue))
            .ToList();
        return NullIfBlank(string.Join(ListSeparator, texts));
    }

    private static string? BooleanText(bool? value) => value switch
    {
        true => TrueText,
        false => FalseText,
        null => null,
    };

    private static string? Blanks(IEnumerable<(string? Id, string? Text)>? blanks)
    {
        var parts = blanks?
            .Where(x => x.Id is not null && x.Text is not null)
            .Select(x => $"[[{x.Id}]] {x.Text}")
            .ToList();
        return parts is null ? null : NullIfBlank(string.Join(ListSeparator, parts));
    }

    private static string? MathStepsText(MathStepsAnswer? answer)
    {
        if (answer is null)
        {
            return null;
        }

        var parts = (answer.Steps ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .ToList();
        if (!string.IsNullOrWhiteSpace(answer.FinalAnswer))
        {
            parts.Add(FinalAnswerLabel + answer.FinalAnswer.Trim());
        }

        return NullIfBlank(string.Join(StepSeparator, parts));
    }

    private static string? NullIfBlank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;

    private static T? Read<T>(string json) where T : class => JsonSerializer.Deserialize<T>(json, QuestionJson.SerializerOptions);

    private static T? Read<T>(JsonNode? node) where T : class => node?.Deserialize<T>(QuestionJson.SerializerOptions);
}
