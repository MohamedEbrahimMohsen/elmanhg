using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Application.TeacherThreads.Shared;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.TeacherThreads.Shared;

public sealed class AskTeacherGateTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly EntitlementResult BaseOnly = new(PlanTier.Base, false, true, null, 50, null, 0, []);
    private static readonly EntitlementResult AskTeacher = new(PlanTier.Base, true, true, null, 50, null, 20, []);
    private readonly ITeacherThreadRepository _teacherThreadRepository = Substitute.For<ITeacherThreadRepository>();
    private readonly SubscriptionsOptions _options = new();
    private readonly Guid _studentId = Guid.NewGuid();

    [Fact]
    public void CurrentQuotaMonth_JustAfterCairoMidnightOnTheFirst_ReturnsTheNewLocalMonthInUtc()
    {
        var (start, end) = AskTeacherGate.CurrentQuotaMonth(new DateTimeOffset(2026, 1, 31, 22, 30, 0, TimeSpan.Zero), "Africa/Cairo");

        (start, end).Should().Be((new DateTimeOffset(2026, 1, 31, 22, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 2, 28, 22, 0, 0, TimeSpan.Zero)));
        (start.Offset, end.Offset).Should().Be((TimeSpan.Zero, TimeSpan.Zero));
    }

    [Fact]
    public void CurrentQuotaMonth_December_EndsOnTheFirstOfJanuary()
    {
        var (start, end) = AskTeacherGate.CurrentQuotaMonth(new DateTimeOffset(2026, 12, 15, 10, 0, 0, TimeSpan.Zero), "Africa/Cairo");

        (start, end).Should().Be((new DateTimeOffset(2026, 11, 30, 22, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 12, 31, 22, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public async Task EnsureCanAskAsync_WithoutAskTeacher_ThrowsAskTeacherRequiresSubscription()
    {
        var act = () => AskTeacherGate.EnsureCanAskAsync(BaseOnly, _studentId, _teacherThreadRepository, _options, Now, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ForbiddenCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.AskTeacherRequiresSubscription);
    }

    [Fact]
    public async Task EnsureCanAskAsync_UsedBelowLimit_Completes()
    {
        StubUsed(19);

        var act = () => AskTeacherGate.EnsureCanAskAsync(AskTeacher, _studentId, _teacherThreadRepository, _options, Now, TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureCanAskAsync_UsedAtLimit_ThrowsMonthlyLimitReachedWithLimit()
    {
        StubUsed(20);

        var act = () => AskTeacherGate.EnsureCanAskAsync(AskTeacher, _studentId, _teacherThreadRepository, _options, Now, TestContext.Current.CancellationToken);

        var exception = (await act.Should().ThrowAsync<ForbiddenCoreException>()).Which;
        exception.ErrorCode.Should().Be(ErrorCodes.AskTeacherMonthlyLimitReached);
        exception.Context!["limit"].Should().Be(20);
    }

    private void StubUsed(int used)
    {
        var threads = Enumerable.Range(0, used)
            .Select(_ => new TeacherThreadBuilder().ForStudent(_studentId).SubmittedAt(Now).Build())
            .Append(new TeacherThreadBuilder().ForStudent(_studentId).SubmittedAt(Now.AddMonths(-1)).Build())
            .Append(new TeacherThreadBuilder().ForStudent(_studentId).SubmittedAt(Now.AddMonths(1)).Build())
            .Append(new TeacherThreadBuilder().SubmittedAt(Now).Build())
            .ToList();
        _teacherThreadRepository.CountAsync(Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<TeacherThread, bool>>>()).Returns(call => threads.Count(call.Arg<Expression<Func<TeacherThread, bool>>>().Compile()));
    }
}
