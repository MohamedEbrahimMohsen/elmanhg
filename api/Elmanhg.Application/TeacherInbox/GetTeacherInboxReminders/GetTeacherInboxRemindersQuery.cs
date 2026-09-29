using Elmanhg.Application.TeacherInbox.Shared;
using MediatR;

namespace Elmanhg.Application.TeacherInbox.GetTeacherInboxReminders;

public sealed record GetTeacherInboxRemindersQuery : IRequest<List<TeacherInboxReminderResult>>;
