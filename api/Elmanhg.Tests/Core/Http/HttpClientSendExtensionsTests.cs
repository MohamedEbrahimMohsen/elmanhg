using Core.Http;
using Elmanhg.Tests.Infrastructure.OtpDelivery;
using FluentAssertions;
using System.Net;

namespace Elmanhg.Tests.Core.Http;

public sealed class HttpClientSendExtensionsTests
{
    private readonly StubHttpMessageHandler _handler = new();

    [Fact]
    public async Task TrySendAsync_SuccessStatus_ReturnsSucceeded()
    {
        var result = await SendAsync(TestContext.Current.CancellationToken);

        result.Failure.Should().Be(HttpCallFailure.None);
        result.StatusCode.Should().Be(200);
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task TrySendAsync_ErrorStatus_ReturnsRejectedWithStatus()
    {
        _handler.StatusCode = HttpStatusCode.UnprocessableEntity;

        var result = await SendAsync(TestContext.Current.CancellationToken);

        result.Failure.Should().Be(HttpCallFailure.Rejected);
        result.StatusCode.Should().Be(422);
    }

    [Fact]
    public async Task TrySendAsync_TransportFailure_ReturnsUnreachable()
    {
        var failure = new HttpRequestException("connection refused");
        _handler.Throw = failure;

        var result = await SendAsync(TestContext.Current.CancellationToken);

        result.Failure.Should().Be(HttpCallFailure.Unreachable);
        result.Exception.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task TrySendAsync_CallerCancels_ThrowsOperationCanceled()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        _handler.Throw = new OperationCanceledException(cancellation.Token);

        var act = () => SendAsync(cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task TrySendAsync_NonTransientException_Propagates()
    {
        _handler.Throw = new InvalidOperationException("not transient");

        var act = () => SendAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private async Task<HttpSendResult> SendAsync(CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient(_handler, disposeHandler: false) { BaseAddress = new Uri("https://provider.test/") };
        using var request = new HttpRequestMessage(HttpMethod.Post, "send");
        return await httpClient.TrySendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
