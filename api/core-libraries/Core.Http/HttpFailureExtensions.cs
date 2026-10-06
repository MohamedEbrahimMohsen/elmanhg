using Polly;

namespace Core.Http;

public static class HttpFailureExtensions
{
    public static bool IsTransientFailure(this Exception exception, CancellationToken cancellationToken) => exception is HttpRequestException or ExecutionRejectedException || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested);
}
