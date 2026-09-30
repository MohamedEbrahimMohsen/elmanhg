using Elmanhg.Application.Shared.Options;
using Elmanhg.Infrastructure.AiService;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Infrastructure.AiService;

public sealed class MathCheckRateLimiterTests : IDisposable
{
    private readonly MathCheckRateLimiter _limiter = new(Options.Create(new MathStepGradingOptions { CheckPermitLimit = 2, CheckWindowSeconds = 3600 }));
    private readonly Guid _studentId = Guid.NewGuid();

    [Fact]
    public void TryAcquire_WithinLimit_ReturnsTrue()
    {
        (_limiter.TryAcquire(_studentId), _limiter.TryAcquire(_studentId)).Should().Be((true, true));
    }

    [Fact]
    public void TryAcquire_OverLimit_ReturnsFalse()
    {
        _limiter.TryAcquire(_studentId);
        _limiter.TryAcquire(_studentId);

        _limiter.TryAcquire(_studentId).Should().BeFalse();
    }

    [Fact]
    public void TryAcquire_OtherStudent_HasOwnWindow()
    {
        _limiter.TryAcquire(_studentId);
        _limiter.TryAcquire(_studentId);

        _limiter.TryAcquire(Guid.NewGuid()).Should().BeTrue();
    }

    public void Dispose() => _limiter.Dispose();
}
