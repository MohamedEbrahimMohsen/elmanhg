using Elmanhg.Application.Questions.Shared;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Schemas;
using System.Text.Json;

namespace Elmanhg.Application.Questions.GradeQuestionDraft;

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
            _ => false,
        };
    }
}
