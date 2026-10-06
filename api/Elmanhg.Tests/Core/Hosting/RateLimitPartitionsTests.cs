using Core.Hosting.RateLimiting;
using Core.Identity.Tokens.CurrentUser;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using System.Net;
using System.Security.Claims;

namespace Elmanhg.Tests.Core.Hosting;

public sealed class RateLimitPartitionsTests
{
    private static readonly IPAddress ClientAddress = IPAddress.Parse("203.0.113.9");

    [Fact]
    public void PerClientIp_KnownAddress_PartitionsByAddress()
    {
        var partition = RateLimitPartitions.PerClientIp(Context(address: ClientAddress), 10, 60);

        partition.PartitionKey.Should().Be("203.0.113.9");
    }

    [Fact]
    public void PerClientIp_NoAddress_UsesUnknownPartition()
    {
        var partition = RateLimitPartitions.PerClientIp(Context(), 10, 60);

        partition.PartitionKey.Should().Be("unknown");
    }

    [Fact]
    public void PerUser_AuthenticatedStudent_PartitionsByUserId()
    {
        var userId = Guid.NewGuid();

        var partition = RateLimitPartitions.PerUser(Context(userId, ClientAddress), 10, 60);

        partition.PartitionKey.Should().Be($"user:{userId}");
    }

    [Fact]
    public void PerUser_NoUserClaim_FallsBackToClientIp()
    {
        var partition = RateLimitPartitions.PerUser(Context(address: ClientAddress), 10, 60);

        partition.PartitionKey.Should().Be("ip:203.0.113.9");
    }

    [Fact]
    public void FixedWindow_PermitLimitReached_RejectsNextLease()
    {
        var partition = RateLimitPartitions.FixedWindow("k", 1, 60);
        using var limiter = partition.Factory(partition.PartitionKey);

        using var first = limiter.AttemptAcquire();
        using var second = limiter.AttemptAcquire();

        (first.IsAcquired, second.IsAcquired).Should().Be((true, false));
    }

    private static DefaultHttpContext Context(Guid? userId = null, IPAddress? address = null)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = address;
        if (userId is { } id)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(CurrentUserService.Constants.UserIdClaimType, id.ToString())], "Bearer"));
        }

        return context;
    }
}
