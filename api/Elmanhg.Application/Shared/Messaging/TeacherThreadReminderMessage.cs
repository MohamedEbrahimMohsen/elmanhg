namespace Elmanhg.Application.Shared.Messaging;

public sealed record TeacherThreadReminderMessage(Guid RecipientUserId, string Address, string TeacherName, Guid ThreadId, string SubjectName, string LessonName, DateTimeOffset SlaDueAt) : OutboundMessage(RecipientUserId, Address);
