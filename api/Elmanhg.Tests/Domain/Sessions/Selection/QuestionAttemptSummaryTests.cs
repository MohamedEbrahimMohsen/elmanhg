using Elmanhg.Domain.Sessions.Selection;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Sessions.Selection;

public sealed class QuestionAttemptSummaryTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Bucket_NeverCorrect_IsLastWrong()
    {
        var summary = new QuestionAttemptSummary(Guid.NewGuid(), 2, 0, T0, null);

        var bucket = summary.Bucket;

        bucket.Should().Be(QuestionSelectionBucket.LastWrong);
        summary.IsLastAttemptCorrect.Should().BeFalse();
    }

    [Fact]
    public void Bucket_CorrectBeforeLatestWrong_IsLastWrong()
    {
        var summary = new QuestionAttemptSummary(Guid.NewGuid(), 2, 1, T0.AddMinutes(5), T0);

        var bucket = summary.Bucket;

        bucket.Should().Be(QuestionSelectionBucket.LastWrong);
    }

    [Fact]
    public void Bucket_LatestCorrectAndCorrectOnce_IsCorrectOnce()
    {
        var summary = new QuestionAttemptSummary(Guid.NewGuid(), 2, 1, T0, T0);

        var bucket = summary.Bucket;

        bucket.Should().Be(QuestionSelectionBucket.CorrectOnce);
    }

    [Fact]
    public void Bucket_LatestCorrectAndCorrectTwice_IsRest()
    {
        var summary = new QuestionAttemptSummary(Guid.NewGuid(), 3, 2, T0, T0);

        var bucket = summary.Bucket;

        bucket.Should().Be(QuestionSelectionBucket.Rest);
    }
}
