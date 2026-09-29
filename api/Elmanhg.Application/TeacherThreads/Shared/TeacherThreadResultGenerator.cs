using Elmanhg.Domain.TeacherThreads;

namespace Elmanhg.Application.TeacherThreads.Shared;

public static class TeacherThreadResultGenerator
{
    public static TeacherThreadResult Generate(TeacherThread thread, DateTimeOffset now)
    {
        var messages = thread.Messages
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Select(x => GenerateMessage(thread, x))
            .ToList();
        return new TeacherThreadResult(thread.Id, GenerateContext(thread.ReadContext()), thread.Status, thread.IsOverdueAt(now), thread.SubmittedAt, thread.SlaDueAt, messages);
    }

    public static TeacherThreadSummaryResult GenerateSummary(TeacherThread thread, DateTimeOffset now)
    {
        var context = thread.ReadContext();
        var question = thread.Messages
            .Where(x => x.SenderId == thread.StudentId)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .First();
        return new TeacherThreadSummaryResult(thread.Id, context.SubjectName, context.LessonName, question.Text, thread.Status, thread.IsOverdueAt(now), thread.SubmittedAt, thread.SlaDueAt);
    }

    public static TeacherThreadContextResult GenerateContext(TeacherThreadContext context) => new(context.SubjectId, context.SubjectName, context.UnitId, context.UnitName, context.LessonId, context.LessonName, context.QuestionId, context.QuestionVersion, context.QuestionStem, context.AttemptId);

    private static TeacherMessageResult GenerateMessage(TeacherThread thread, TeacherMessage message) => new(message.Id, message.SenderId == thread.StudentId, message.Kind, message.Text, message.ImageUrl, message.CreatedAt);
}
