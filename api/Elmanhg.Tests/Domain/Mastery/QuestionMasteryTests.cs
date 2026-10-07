using Elmanhg.Domain.Mastery;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Mastery;

public sealed class QuestionMasteryTests
{
    private const decimal Threshold = 0.8m;
    private static readonly DateTimeOffset T0 = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _questionId = Guid.NewGuid();

    [Fact]
    public void Start_FirstAttempt_KeepsLatestAndIsNotMastered()
    {
        var attempt = Attempt(1m, 0);

        var mastery = QuestionMastery.Start(_studentId, _questionId, attempt);

        (mastery.StudentId, mastery.QuestionId).Should().Be((_studentId, _questionId));
        (mastery.LatestAttemptId, mastery.LatestNormalisedScore, mastery.LatestAttemptedAt).Should().Be((attempt.AttemptId, 1m, T0));
        mastery.PreviousAttemptId.Should().BeNull();
        mastery.PreviousNormalisedScore.Should().BeNull();
        mastery.PreviousAttemptedAt.Should().BeNull();
        mastery.IsMastered.Should().BeFalse();
    }

    [Fact]
    public void Record_TwoCorrectAttempts_IsMastered()
    {
        var first = Attempt(1m, 0);
        var second = Attempt(0.9m, 1);
        var mastery = QuestionMastery.Start(_studentId, _questionId, first);

        mastery.Record(second, Threshold);

        mastery.IsMastered.Should().BeTrue();
        (mastery.LatestAttemptId, mastery.PreviousAttemptId).Should().Be((second.AttemptId, first.AttemptId));
    }

    [Fact]
    public void Record_BothAtThreshold_IsMastered()
    {
        var mastery = Replay(0.8m, 0.8m);

        mastery.IsMastered.Should().BeTrue();
    }

    [Fact]
    public void Record_OneBelowThreshold_IsNotMastered()
    {
        var mastery = Replay(1m, 0.79m);

        mastery.IsMastered.Should().BeFalse();
    }

    [Fact]
    public void Record_WrongAfterMastered_LosesMastery()
    {
        var mastery = Replay(1m, 1m, 0m);

        mastery.IsMastered.Should().BeFalse();
    }

    [Fact]
    public void Record_CorrectWrongCorrect_NeedsOneMoreCorrect()
    {
        var mastery = Replay(1m, 0m, 1m);
        var afterThree = mastery.IsMastered;

        mastery.Record(Attempt(1m, 3), Threshold);

        afterThree.Should().BeFalse();
        mastery.IsMastered.Should().BeTrue();
    }

    [Fact]
    public void Record_SameAttemptAgain_ChangesNothing()
    {
        var first = Attempt(1m, 0);
        var second = Attempt(1m, 1);
        var mastery = QuestionMastery.Start(_studentId, _questionId, first);
        mastery.Record(second, Threshold);
        var updatedAt = mastery.UpdationDate;

        mastery.Record(second, Threshold);

        (mastery.LatestAttemptId, mastery.PreviousAttemptId, mastery.PreviousNormalisedScore, mastery.PreviousAttemptedAt).Should().Be((second.AttemptId, first.AttemptId, 1m, T0));
        mastery.IsMastered.Should().BeTrue();
        mastery.UpdationDate.Should().Be(updatedAt);
    }

    [Fact]
    public void Record_OlderThanLatest_BecomesPrevious()
    {
        var latest = Attempt(1m, 2);
        var older = Attempt(1m, 1);
        var mastery = QuestionMastery.Start(_studentId, _questionId, latest);

        mastery.Record(older, Threshold);

        (mastery.LatestAttemptId, mastery.PreviousAttemptId, mastery.PreviousAttemptedAt).Should().Be((latest.AttemptId, older.AttemptId, T0.AddMinutes(1)));
        mastery.IsMastered.Should().BeTrue();
    }

    [Fact]
    public void Record_OlderThanBothSlots_IsIgnored()
    {
        var previous = Attempt(1m, 2);
        var latest = Attempt(1m, 3);
        var mastery = QuestionMastery.Start(_studentId, _questionId, previous);
        mastery.Record(latest, Threshold);

        mastery.Record(Attempt(0m, 1), Threshold);

        (mastery.LatestAttemptId, mastery.PreviousAttemptId).Should().Be((latest.AttemptId, previous.AttemptId));
        mastery.IsMastered.Should().BeTrue();
    }

    [Fact]
    public void Record_NewAttempt_LeavesUpdatedByToTheSave()
    {
        var mastery = QuestionMastery.Start(_studentId, _questionId, Attempt(1m, 0));
        mastery.UpdatedBy = null;

        mastery.Record(Attempt(1m, 1), Threshold);

        mastery.UpdatedBy.Should().BeNull();
    }

    [Fact]
    public void Record_ThresholdArgument_IsHonoured()
    {
        var mastery = QuestionMastery.Start(_studentId, _questionId, Attempt(0.85m, 0));

        mastery.Record(Attempt(0.85m, 1), 0.9m);

        mastery.IsMastered.Should().BeFalse();
    }

    private static MasteryAttempt Attempt(decimal score, int minutes) => new(Guid.NewGuid(), score, T0.AddMinutes(minutes));

    private QuestionMastery Replay(params decimal[] scores)
    {
        var mastery = QuestionMastery.Start(_studentId, _questionId, Attempt(scores[0], 0));
        for (var index = 1; index < scores.Length; index++)
        {
            mastery.Record(Attempt(scores[index], index), Threshold);
        }

        return mastery;
    }
}
