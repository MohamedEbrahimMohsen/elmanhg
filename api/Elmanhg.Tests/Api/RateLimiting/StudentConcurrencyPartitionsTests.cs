using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Api.RateLimiting;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace Elmanhg.Tests.Api.RateLimiting;

public sealed class StudentConcurrencyPartitionsTests : IDisposable
{
    private readonly PartitionedRateLimiter<HttpContext> _limiter = PartitionedRateLimiter.Create<HttpContext, string>(x => StudentConcurrencyPartitions.ConcurrentStudentRequests(x, 1));
    private readonly List<RateLimitLease> _leases = [];

    [Fact]
    public void ConcurrentStudentRequests_AvatarRequestWhileOneInFlight_IsRejected()
    {
        var userId = Guid.NewGuid();

        var first = Acquire(Context(userId, policy: StudentRateLimitPolicies.AvatarMessages));
        var second = Acquire(Context(userId, policy: StudentRateLimitPolicies.AvatarMessages));

        (first.IsAcquired, second.IsAcquired).Should().Be((true, false));
    }

    [Fact]
    public void ConcurrentStudentRequests_FirstLeaseReleased_NextRequestIsAcquired()
    {
        var userId = Guid.NewGuid();
        Acquire(Context(userId, policy: StudentRateLimitPolicies.AvatarMessages)).Dispose();

        var next = Acquire(Context(userId, policy: StudentRateLimitPolicies.AvatarMessages));

        next.IsAcquired.Should().BeTrue();
    }

    [Fact]
    public void ConcurrentStudentRequests_DifferentStudents_AreAcquiredTogether()
    {
        var first = Acquire(Context(Guid.NewGuid(), policy: StudentRateLimitPolicies.AskTeacherSubmissions));
        var second = Acquire(Context(Guid.NewGuid(), policy: StudentRateLimitPolicies.AskTeacherSubmissions));

        (first.IsAcquired, second.IsAcquired).Should().Be((true, true));
    }

    [Fact]
    public void ConcurrentStudentRequests_AvatarAndAskTeacherForSameStudent_UseSeparatePartitions()
    {
        var userId = Guid.NewGuid();
        Acquire(Context(userId, policy: StudentRateLimitPolicies.AvatarMessages));

        var askTeacher = Acquire(Context(userId, policy: StudentRateLimitPolicies.AskTeacherSubmissions));

        askTeacher.IsAcquired.Should().BeTrue();
    }

    [Fact]
    public void ConcurrentStudentRequests_UntaggedEndpoint_IsNeverLimited()
    {
        var userId = Guid.NewGuid();

        var leases = Enumerable.Range(0, 3)
            .Select(_ => Acquire(Context(userId, policy: AuthRateLimitPolicies.Credentials)))
            .ToList();

        leases.Should().OnlyContain(x => x.IsAcquired);
    }

    [Fact]
    public void ConcurrentStudentRequests_NoEndpoint_IsNeverLimited()
    {
        var userId = Guid.NewGuid();

        var first = Acquire(Context(userId));
        var second = Acquire(Context(userId));

        (first.IsAcquired, second.IsAcquired).Should().Be((true, true));
    }

    public void Dispose()
    {
        _leases.ForEach(x => x.Dispose());
        _limiter.Dispose();
    }

    private RateLimitLease Acquire(HttpContext context)
    {
        var lease = _limiter.AttemptAcquire(context);
        _leases.Add(lease);
        return lease;
    }

    private static DefaultHttpContext Context(Guid? userId = null, IPAddress? address = null, string? policy = null)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = address;
        if (userId is { } id)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(CurrentUserService.Constants.UserIdClaimType, id.ToString())], "Bearer"));
        }

        if (policy is not null)
        {
            context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new EnableRateLimitingAttribute(policy)), policy));
        }

        return context;
    }
}
