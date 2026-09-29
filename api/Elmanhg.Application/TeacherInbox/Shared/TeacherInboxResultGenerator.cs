using Elmanhg.Application.TeacherThreads.Shared;
using Elmanhg.Domain.TeacherThreads;

namespace Elmanhg.Application.TeacherInbox.Shared;

public static class TeacherInboxResultGenerator
{
    public static TeacherInboxItemResult GenerateItem(TeacherThread thread, IReadOnlyDictionary<Guid, string> names, Guid callerId, DateTimeOffset now)
    {
        var context = thread.ReadContext();
        return new TeacherInboxItemResult(thread.Id, context.SubjectName, context.LessonName, TeacherThreadResultGenerator.QuestionText(thread), StudentName(thread, names), TeacherName(thread, names), thread.IsClaimedBy(callerId), thread.Status, thread.IsOverdueAt(now), thread.SubmittedAt, thread.SlaDueAt);
    }

    public static TeacherInboxThreadResult GenerateThread(TeacherThread thread, IReadOnlyDictionary<Guid, string> names, Guid callerId, DateTimeOffset now)
    {
        var canClaim = thread.TeacherId == null && thread.Status != TeacherThreadStatus.Closed;
        var canReply = thread.IsClaimedBy(callerId) && thread.Status == TeacherThreadStatus.Open;
        return new TeacherInboxThreadResult(thread.Id, TeacherThreadResultGenerator.GenerateContext(thread.ReadContext()), StudentName(thread, names), TeacherName(thread, names), thread.IsClaimedBy(callerId), canClaim, canReply, thread.Status, thread.IsOverdueAt(now), thread.SubmittedAt, thread.SlaDueAt, thread.ClaimedAt, TeacherThreadResultGenerator.GenerateMessages(thread), thread.Rating);
    }

    public static TeacherInboxReminderResult GenerateReminder(TeacherThread thread, TeacherThreadSlaEventKind kind, Guid callerId, DateTimeOffset now)
    {
        var context = thread.ReadContext();
        return new TeacherInboxReminderResult(thread.Id, context.SubjectName, context.LessonName, TeacherThreadResultGenerator.QuestionText(thread), kind, thread.IsClaimedBy(callerId), thread.IsOverdueAt(now), thread.SlaDueAt);
    }

    private static string StudentName(TeacherThread thread, IReadOnlyDictionary<Guid, string> names) => names.GetValueOrDefault(thread.StudentId, string.Empty);

    private static string? TeacherName(TeacherThread thread, IReadOnlyDictionary<Guid, string> names) => thread.TeacherId is { } teacherId ? names.GetValueOrDefault(teacherId) : null;
}
