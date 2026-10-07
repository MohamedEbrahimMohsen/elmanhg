using Core.Errors;
using Core.Hosting;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Threading.RateLimiting;

namespace Elmanhg.Tests.Core.Hosting;

public sealed class CoreRateLimitingTests
{
    [Fact]
    public void AddCoreRateLimiting_Default_RejectsWith429()
    {
        var options = RateLimiterOptions();

        options.RejectionStatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
    }

    [Fact]
    public async Task AddCoreRateLimiting_OnRejected_ThrowsRateLimitExceededWithConfiguredCode()
    {
        var options = RateLimiterOptions();

        var act = async () => await options.OnRejected!(new OnRejectedContext { HttpContext = new DefaultHttpContext(), Lease = Substitute.For<RateLimitLease>() }, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<RateLimitExceededCoreException>()).Which.ErrorCode.Should().Be("PROBE_LIMIT");
    }

    private static RateLimiterOptions RateLimiterOptions() => new ServiceCollection()
        .AddCoreRateLimiting("PROBE_LIMIT")
        .BuildServiceProvider()
        .GetRequiredService<IOptions<RateLimiterOptions>>()
        .Value;
}
