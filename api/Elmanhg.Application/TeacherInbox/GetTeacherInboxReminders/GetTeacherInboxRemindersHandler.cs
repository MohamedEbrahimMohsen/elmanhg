using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.TeacherInbox.Shared;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Teachers;
using Elmanhg.Domain.TeacherThreads;
using MediatR;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Elmanhg.Application.TeacherInbox.GetTeacherInboxReminders;

public sealed class GetTeacherInboxRemindersHandler(ITeacherThreadRepository teacherThreadRepository, ITeacherThreadSlaEventRepository teacherThreadSlaEventRepository, ITeacherSubjectRepository teacherSubjectRepository, IOptions<AskTeacherOptions> askTeacherOptions, TimeProvider timeProvider, ICurrentUserService currentUserService) : IRequestHandler<GetTeacherInboxRemindersQuery, List<TeacherInboxReminderResult>>
{
    public async Task<List<TeacherInboxReminderResult>> Handle(GetTeacherInboxRemindersQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);
        var isAdmin = currentUserService.GetClaim(ClaimTypes.Role) == nameof(UserRole.Admin);
        List<Guid>? subjectIds = null;
        if (!isAdmin)
        {
            var assignments = await teacherSubjectRepository.FindAsync(x => x.TeacherId == userId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
            subjectIds = assignments
                .Select(x => x.SubjectId)
                .ToList();
        }

        var threads = await teacherThreadRepository.GetRemindedOpenThreadsAsync(subjectIds, userId, askTeacherOptions.Value.ReminderListMaxCount, cancellationToken).ConfigureAwait(false);
        var ids = threads
            .Select(x => x.Id)
            .ToList();
        var events = await teacherThreadSlaEventRepository.FindAsync(x => ids.Contains(x.ThreadId) && x.Kind != TeacherThreadSlaEventKind.Breach, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();

        return threads
            .Select(thread => TeacherInboxResultGenerator.GenerateReminder(thread, LatestReminder(events, thread), userId, now))
            .ToList();
    }

    private static TeacherThreadSlaEventKind LatestReminder(List<TeacherThreadSlaEvent> events, TeacherThread thread) => events
        .Where(x => x.ThreadId == thread.Id && x.WindowStartedAt == thread.SlaWindowStartedAt)
        .Max(x => x.Kind);
}
