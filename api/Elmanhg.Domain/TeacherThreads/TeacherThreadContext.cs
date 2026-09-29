using Elmanhg.Domain.Questions.Schemas;
using System.Text.Json;

namespace Elmanhg.Domain.TeacherThreads;

public sealed record TeacherThreadContext(Guid SubjectId, string SubjectName, Guid UnitId, string UnitName, Guid LessonId, string LessonName, Guid? QuestionId, int? QuestionVersion, string? QuestionStem, Guid? AttemptId)
{
    public string ToJson() => JsonSerializer.Serialize(this, QuestionJson.SerializerOptions);

    public static TeacherThreadContext FromJson(string json) => JsonSerializer.Deserialize<TeacherThreadContext>(json, QuestionJson.SerializerOptions) ?? throw new InvalidOperationException("Teacher thread context is empty.");
}
