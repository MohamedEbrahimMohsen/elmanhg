using Elmanhg.Application.Shared.Realtime;
using Elmanhg.Domain.TeacherThreads;
using Microsoft.AspNetCore.SignalR;

namespace Elmanhg.Api.Realtime;

public sealed class SignalRTeacherThreadNotifier(IHubContext<NotificationsHub> hubContext, ILogger<SignalRTeacherThreadNotifier> logger) : ITeacherThreadNotifier
{
    public Task NotifyReplyAsync(Guid studentId, Guid threadId, CancellationToken cancellationToken)
        => SendBestEffortAsync(() => hubContext.Clients.User(studentId.ToString()), RealtimeEvents.TeacherReplyReceived, new TeacherReplyReceivedMessage(threadId), threadId, cancellationToken);

    public Task NotifyReminderAsync(IReadOnlyCollection<Guid> teacherIds, Guid threadId, TeacherThreadSlaEventKind kind, CancellationToken cancellationToken)
    {
        var userIds = teacherIds
            .Select(x => x.ToString())
            .ToList();
        return SendBestEffortAsync(() => hubContext.Clients.Users(userIds), RealtimeEvents.TeacherThreadReminder, new TeacherThreadReminderMessage(threadId, kind), threadId, cancellationToken);
    }

    // Pushes run after the change is committed, so a failed push must never turn that saved change into an error response.
    private async Task SendBestEffortAsync(Func<IClientProxy> clients, string eventName, object message, Guid threadId, CancellationToken cancellationToken)
    {
        try
        {
            await clients().SendAsync(eventName, message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Realtime push {EventName} for thread {ThreadId} failed.", eventName, threadId);
        }
    }
}
