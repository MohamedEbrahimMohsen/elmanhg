namespace Elmanhg.Application.Shared.Realtime;

public interface IGradeReviewNotifier
{
    Task NotifyReviewedAsync(Guid studentId, Guid sessionId, Guid questionId, CancellationToken cancellationToken);
}
