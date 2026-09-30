using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Schemas;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Elmanhg.Application.Questions.Shared;

public static class QuestionAnswerRules
{
    public static bool CanRead(QuestionType type, JsonElement answer)
    {
        return type switch
        {
            QuestionType.Mcq => QuestionSchemaReader.TryRead<McqAnswer>(answer, out _),
            QuestionType.Multi => QuestionSchemaReader.TryRead<MultiAnswer>(answer, out _),
            QuestionType.TrueFalse => QuestionSchemaReader.TryRead<TrueFalseAnswer>(answer, out _),
            QuestionType.Fill => QuestionSchemaReader.TryRead<FillAnswer>(answer, out _),
            QuestionType.Short => QuestionSchemaReader.TryRead<ShortAnswer>(answer, out _),
            QuestionType.Essay => QuestionSchemaReader.TryRead<EssayAnswer>(answer, out var essay) && essay.Text is not null,
            QuestionType.MathSteps => MathStepsAnswerRules.CanRead(answer),
            _ => false,
        };
    }

    public static string Canonicalize(QuestionType type, JsonElement answer)
    {
        return type switch
        {
            QuestionType.Mcq => QuestionSchemaReader.Serialize(QuestionSchemaReader.Read<McqAnswer>(answer)),
            QuestionType.Multi => QuestionSchemaReader.Serialize(QuestionSchemaReader.Read<MultiAnswer>(answer)),
            QuestionType.TrueFalse => QuestionSchemaReader.Serialize(QuestionSchemaReader.Read<TrueFalseAnswer>(answer)),
            QuestionType.Fill => QuestionSchemaReader.Serialize(QuestionSchemaReader.Read<FillAnswer>(answer)),
            QuestionType.Short => QuestionSchemaReader.Serialize(QuestionSchemaReader.Read<ShortAnswer>(answer)),
            QuestionType.Essay => QuestionSchemaReader.Serialize(new EssayAnswer(QuestionSchemaReader.Read<EssayAnswer>(answer).Text!.Trim())),
            QuestionType.MathSteps => MathStepsAnswerRules.Canonicalize(answer),
            _ => throw new InvalidOperationException("Unsupported question type."),
        };
    }

    public static bool TryReadWrittenEssay(QuestionType type, JsonElement answer, [NotNullWhen(true)] out string? text)
    {
        text = type == QuestionType.Essay && QuestionSchemaReader.TryRead<EssayAnswer>(answer, out var essay) && !string.IsNullOrWhiteSpace(essay.Text) ? essay.Text.Trim() : null;
        return text is not null;
    }

    public static bool IsEssayTooLong(QuestionType type, JsonElement answer, int maxLength) => type == QuestionType.Essay && QuestionSchemaReader.Read<EssayAnswer>(answer).Text!.Length > maxLength;

    public static bool IsRawAnswerTooLong(QuestionType type, JsonElement answer, SessionsOptions options) => answer.GetRawText().Length > RawAnswerMaxLength(type, options);

    public static bool ExceedsLimits(QuestionType type, JsonElement answer, SessionsOptions options) => type == QuestionType.MathSteps && MathStepsAnswerRules.ExceedsLimits(answer, options);

    private static int RawAnswerMaxLength(QuestionType type, SessionsOptions options) => type switch
    {
        QuestionType.Essay => options.EssayAnswerMaxLength,
        QuestionType.MathSteps => options.MathStepsAnswerMaxLength,
        _ => options.AnswerMaxLength,
    };
}
