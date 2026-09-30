using Elmanhg.Application.Shared.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace Elmanhg.Api.Realtime;

public sealed class SignalRGradeReviewNotifier(IHubContext<NotificationsHub> hubContext, ILogger<SignalRGradeReviewNotifier> logger) : IGradeReviewNotifier
{
    // Pushes run after the change is committed, so a failed push must never turn that saved change into an error response.
    public async Task NotifyReviewedAsync(Guid studentId, Guid sessionId, Guid questionId, CancellationToken cancellationToken)
    {
        try
        {
            await hubContext.Clients.User(studentId.ToString()).SendAsync(RealtimeEvents.GradeReviewed, new GradeReviewedMessage(sessionId, questionId), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Realtime push {EventName} for session {SessionId} failed.", RealtimeEvents.GradeReviewed, sessionId);
        }
    }
}
