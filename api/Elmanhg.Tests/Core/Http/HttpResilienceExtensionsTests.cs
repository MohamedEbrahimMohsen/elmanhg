using Core.Http;
using Elmanhg.Tests.Infrastructure.OtpDelivery;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using System.Net;

namespace Elmanhg.Tests.Core.Http;

public sealed class HttpResilienceExtensionsTests
{
    private const string ClientName = "probe";

    private readonly StubHttpMessageHandler _handler = new() { StatusCode = HttpStatusCode.InternalServerError };

    [Fact]
    public void AddTimeoutResilience_Budget_SetsAttemptAndTotalTimeouts()
    {
        using var provider = BuildProvider(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), retryUnsafeMethods: false);

        var options = ResilienceOptions(provider);

        options.AttemptTimeout.Timeout.Should().Be(TimeSpan.FromSeconds(10));
        options.TotalRequestTimeout.Timeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void AddTimeoutResilience_LongAttempt_WidensSamplingToTwiceAttempt()
    {
        using var provider = BuildProvider(TimeSpan.FromSeconds(45), TimeSpan.FromSeconds(45), retryUnsafeMethods: false);

        ResilienceOptions(provider).CircuitBreaker.SamplingDuration.Should().Be(TimeSpan.FromSeconds(90));
    }

    [Fact]
    public void AddTimeoutResilience_ShortAttempt_KeepsDefaultSampling()
    {
        using var provider = BuildProvider(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30), retryUnsafeMethods: false);

        ResilienceOptions(provider).CircuitBreaker.SamplingDuration.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task AddTimeoutResilience_UnsafeRetryDisabled_PostServerErrorMakesOneAttempt()
    {
        using var provider = BuildProvider(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), retryUnsafeMethods: false);

        using var response = await PostAsync(provider);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        _handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task AddTimeoutResilience_UnsafeRetryEnabled_PostServerErrorRetries()
    {
        using var provider = BuildProvider(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), retryUnsafeMethods: true);

        using var response = await PostAsync(provider);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        _handler.CallCount.Should().BeGreaterThan(1);
    }

    private static HttpStandardResilienceOptions ResilienceOptions(ServiceProvider provider) => provider.GetRequiredService<IOptionsMonitor<HttpStandardResilienceOptions>>().Get($"{ClientName}-standard");

    private static Task<HttpResponseMessage> PostAsync(ServiceProvider provider) => provider.GetRequiredService<IHttpClientFactory>().CreateClient(ClientName).PostAsync("https://provider.test/send", new StringContent("{}"), TestContext.Current.CancellationToken);

    private ServiceProvider BuildProvider(TimeSpan attempt, TimeSpan total, bool retryUnsafeMethods)
    {
        var services = new ServiceCollection();
        services.AddHttpClient(ClientName).ConfigurePrimaryHttpMessageHandler(() => _handler).AddTimeoutResilience(_ => attempt, _ => total, retryUnsafeMethods);
        services.PostConfigure<HttpStandardResilienceOptions>($"{ClientName}-standard", resilience => resilience.Retry.Delay = TimeSpan.Zero);
        return services.BuildServiceProvider();
    }
}
