using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subscriptions;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Subscriptions.Shared;

public sealed class FreeTierGateTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Created = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly EntitlementResult Free = new(PlanTier.Free, false, false, 10, 5, 1, 0, []);
    private static readonly EntitlementResult Base = new(PlanTier.Base, false, true, null, 50, null, 0, []);
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly SubscriptionsOptions _options = new();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly LessonPosition _first;
    private readonly LessonPosition _second;

    public FreeTierGateTests()
    {
        var unitId = Guid.NewGuid();
        _first = new LessonPosition(Guid.NewGuid(), unitId, 1, Created);
        _second = new LessonPosition(Guid.NewGuid(), unitId, 2, Created);
        _lessonRepository.GetPublishedSiblingPositionsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([_first, _second]);
    }

    [Fact]
    public void EnsureCanTakeExams_Free_ThrowsExamRequiresSubscription()
    {
        var act = () => FreeTierGate.EnsureCanTakeExams(Free);

        act.Should().Throw<ForbiddenCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.ExamRequiresSubscription);
    }

    [Fact]
    public void EnsureCanTakeExams_Base_DoesNotThrow()
    {
        var act = () => FreeTierGate.EnsureCanTakeExams(Base);

        act.Should().NotThrow();
    }

    [Fact]
    public async Task EnsureLessonOpenAsync_FreeFirstLesson_DoesNotThrow()
    {
        var act = () => FreeTierGate.EnsureLessonOpenAsync(Free, _first.Id, _lessonRepository, TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureLessonOpenAsync_FreeSecondLesson_ThrowsLessonLocked()
    {
        var act = () => FreeTierGate.EnsureLessonOpenAsync(Free, _second.Id, _lessonRepository, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ForbiddenCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonLocked);
    }

    [Fact]
    public async Task EnsureLessonOpenAsync_BaseSecondLesson_DoesNotThrow()
    {
        var act = () => FreeTierGate.EnsureLessonOpenAsync(Base, _second.Id, _lessonRepository, TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureQuizQuestionAvailableAsync_FreeBelowLimit_DoesNotThrow()
    {
        StubCount(9);

        var act = () => FreeTierGate.EnsureQuizQuestionAvailableAsync(Free, _studentId, _sessionRepository, _options, Now, TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureQuizQuestionAvailableAsync_FreeAtLimit_ThrowsWithLimitContext()
    {
        StubCount(10);

        var act = () => FreeTierGate.EnsureQuizQuestionAvailableAsync(Free, _studentId, _sessionRepository, _options, Now, TestContext.Current.CancellationToken);

        var exception = (await act.Should().ThrowAsync<ForbiddenCoreException>()).Which;
        exception.ErrorCode.Should().Be(ErrorCodes.QuizDailyLimitReached);
        exception.Context!["limit"].Should().Be(10);
    }

    [Fact]
    public async Task EnsureQuizQuestionAvailableAsync_BaseAboveFreeLimit_DoesNotThrow()
    {
        StubCount(500);

        var act = () => FreeTierGate.EnsureQuizQuestionAvailableAsync(Base, _studentId, _sessionRepository, _options, Now, TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CountQuizQuestionsTodayAsync_LateUtcEvening_UsesCairoLocalDate()
    {
        _sessionRepository.CountQuizAttemptsOnDayAsync(_studentId, "Africa/Cairo", new DateOnly(2026, 10, 2), Arg.Any<CancellationToken>()).Returns(4);

        var used = await FreeTierGate.CountQuizQuestionsTodayAsync(_studentId, _sessionRepository, _options, new DateTimeOffset(2026, 10, 1, 22, 30, 0, TimeSpan.Zero), TestContext.Current.CancellationToken);

        used.Should().Be(4);
    }

    private void StubCount(int used) => _sessionRepository.CountQuizAttemptsOnDayAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(used);
}
