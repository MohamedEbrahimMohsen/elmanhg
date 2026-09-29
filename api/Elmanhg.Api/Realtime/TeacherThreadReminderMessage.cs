using Elmanhg.Domain.TeacherThreads;

namespace Elmanhg.Api.Realtime;

public sealed record TeacherThreadReminderMessage(Guid ThreadId, TeacherThreadSlaEventKind Kind);
