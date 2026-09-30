namespace Elmanhg.Application.Shared.AiService;

public interface IMathCheckRateLimiter
{
    bool TryAcquire(Guid studentId);
}
