using Elmanhg.Domain.Questions.Schemas;
using System.Text.Json;

namespace Elmanhg.Domain.Sessions;

public sealed record QuizScope(Guid LessonId)
{
    public string ToKey() => $"lesson:{LessonId:D}";

    public string ToJson() => JsonSerializer.Serialize(this, QuestionJson.SerializerOptions);

    public static QuizScope FromJson(string json) => JsonSerializer.Deserialize<QuizScope>(json, QuestionJson.SerializerOptions) ?? throw new InvalidOperationException("Quiz scope is empty.");
}
