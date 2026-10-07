using Core.DDD.Models;
using FluentAssertions;

namespace Elmanhg.Tests.Core.DDD;

public sealed class RetryScheduleTests
{
    private const string ErrorCode = "PROBE_FAILED";
    private static readonly DateTimeOffset At = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(30);

    [Fact]
    public void DueAt_NewSchedule_IsDueFromThatTimeWithNoAttempts()
    {
        var schedule = RetrySchedule.DueAt(At);

        (schedule.Attempts, schedule.NextAttemptAt, schedule.LastErrorCode).Should().Be((0, (DateTimeOffset?)At, (string?)null));
        schedule.IsDueAt(At).Should().BeTrue();
        schedule.IsDueAt(At.AddTicks(-TimeSpan.TicksPerMicrosecond)).Should().BeFalse();
    }

    [Fact]
    public void RecordFailure_AttemptsLeft_DoublesTheDelayEachTime()
    {
        var schedule = RetrySchedule.DueAt(At);
        var firstFailedAt = At.AddSeconds(5);
        var secondFailedAt = firstFailedAt.AddSeconds(30);

        var firstExhausted = schedule.RecordFailure(ErrorCode, firstFailedAt, 4, BaseDelay);
        var firstNext = schedule.NextAttemptAt;
        var secondExhausted = schedule.RecordFailure(ErrorCode, secondFailedAt, 4, BaseDelay);

        (firstExhausted, firstNext).Should().Be((false, (DateTimeOffset?)firstFailedAt.AddSeconds(30)));
        (secondExhausted, schedule.NextAttemptAt, schedule.Attempts, schedule.LastErrorCode).Should().Be((false, (DateTimeOffset?)secondFailedAt.AddSeconds(60), 2, (string?)ErrorCode));
    }

    [Fact]
    public void RecordFailure_LastAttempt_ReturnsExhaustedAndClearsNextAttempt()
    {
        var schedule = RetrySchedule.DueAt(At);

        var exhausted = schedule.RecordFailure(ErrorCode, At, 1, BaseDelay);

        exhausted.Should().BeTrue();
        schedule.NextAttemptAt.Should().BeNull();
        schedule.IsDueAt(DateTimeOffset.MaxValue).Should().BeFalse();
        schedule.LastErrorCode.Should().Be(ErrorCode);
    }

    [Fact]
    public void RecordFailure_LongErrorCode_TruncatesToMaxLength()
    {
        var schedule = RetrySchedule.DueAt(At);

        schedule.RecordFailure(new string('X', 150), At, 3, BaseDelay);

        schedule.LastErrorCode.Should().HaveLength(RetrySchedule.ErrorCodeMaxLength).And.HaveLength(100);
    }

    [Fact]
    public void RecordFailure_NullErrorCode_LeavesLastErrorCodeNull()
    {
        var schedule = RetrySchedule.DueAt(At);
        schedule.RecordFailure(ErrorCode, At, 3, BaseDelay);

        schedule.RecordFailure(null, At.AddSeconds(30), 3, BaseDelay);

        schedule.LastErrorCode.Should().BeNull();
    }

    [Fact]
    public void RecordFailure_MaxAttemptsBelowOne_Throws()
    {
        var schedule = RetrySchedule.DueAt(At);

        var act = () => schedule.RecordFailure(ErrorCode, At, 0, BaseDelay);

        act.Should().Throw<ArgumentOutOfRangeException>();
        schedule.Attempts.Should().Be(0);
    }

    [Fact]
    public void RecordSuccess_AfterFailure_CountsAttemptAndClearsScheduleAndError()
    {
        var schedule = RetrySchedule.DueAt(At);
        schedule.RecordFailure(ErrorCode, At, 3, BaseDelay);

        schedule.RecordSuccess();

        (schedule.Attempts, schedule.NextAttemptAt, schedule.LastErrorCode).Should().Be((2, (DateTimeOffset?)null, (string?)null));
    }

    [Fact]
    public void Lease_PushesNextAttemptOutWithoutCountingAnAttempt()
    {
        var schedule = RetrySchedule.DueAt(At);

        schedule.Lease(At, TimeSpan.FromMinutes(30));

        (schedule.NextAttemptAt, schedule.Attempts).Should().Be(((DateTimeOffset?)At.AddMinutes(30), 0));
    }
}
