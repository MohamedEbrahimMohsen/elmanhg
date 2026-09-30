using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Schemas;
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
            _ => throw new InvalidOperationException("Unsupported question type."),
        };
    }
}
