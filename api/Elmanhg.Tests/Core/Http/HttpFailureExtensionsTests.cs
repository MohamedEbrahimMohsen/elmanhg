using Core.Http;
using FluentAssertions;
using Polly.Timeout;

namespace Elmanhg.Tests.Core.Http;

public sealed class HttpFailureExtensionsTests
{
    [Fact]
    public void IsTransientFailure_HttpRequestException_ReturnsTrue()
    {
        new HttpRequestException("connection refused").IsTransientFailure(CancellationToken.None).Should().BeTrue();
    }

    [Fact]
    public void IsTransientFailure_PollyRejection_ReturnsTrue()
    {
        new TimeoutRejectedException().IsTransientFailure(CancellationToken.None).Should().BeTrue();
    }

    [Fact]
    public void IsTransientFailure_TimeoutWithoutCallerCancellation_ReturnsTrue()
    {
        new TaskCanceledException().IsTransientFailure(CancellationToken.None).Should().BeTrue();
    }

    [Fact]
    public void IsTransientFailure_CallerCancelled_ReturnsFalse()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        new OperationCanceledException(cancellation.Token).IsTransientFailure(cancellation.Token).Should().BeFalse();
    }

    [Fact]
    public void IsTransientFailure_OtherException_ReturnsFalse()
    {
        new InvalidOperationException().IsTransientFailure(CancellationToken.None).Should().BeFalse();
    }
}
