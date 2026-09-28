using Elmanhg.Application.Exams.Shared;
using Elmanhg.Domain.Sessions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Exams.Shared;

public sealed class ExamAttemptsResultGeneratorTests
{
    private static readonly DateTimeOffset Day = new(2026, 9, 28, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Generate_NoAttempts_ReturnsNullBestAndEmptyList()
    {
        var result = ExamAttemptsResultGenerator.Generate([]);

        result.BestScorePercent.Should().BeNull();
        result.Attempts.Should().BeEmpty();
    }

    [Fact]
    public void Generate_Attempts_KeepsOrderAndFlagsHighest()
    {
        List<ExamAttemptSummary> attempts = [Summary(70m, 3), Summary(80m, 2), Summary(60.5m, 1)];

        var result = ExamAttemptsResultGenerator.Generate(attempts);

        result.BestScorePercent.Should().Be(80m);
        result.Attempts.Select(x => x.SessionId).Should().Equal(attempts.Select(x => x.SessionId));
        result.Attempts.Select(x => x.IsBest).Should().Equal(false, true, false);
        result.Attempts.Select(x => x.SubmittedAt).Should().Equal(Day.AddDays(3), Day.AddDays(2), Day.AddDays(1));
        result.Attempts.Select(x => x.ScorePercent).Should().Equal(70m, 80m, 60.5m);
    }

    [Fact]
    public void Generate_TiedBest_FlagsEveryTiedAttempt()
    {
        var result = ExamAttemptsResultGenerator.Generate([Summary(80m, 3), Summary(80m, 2), Summary(50m, 1)]);

        result.BestScorePercent.Should().Be(80m);
        result.Attempts.Select(x => x.IsBest).Should().Equal(true, true, false);
    }

    private static ExamAttemptSummary Summary(decimal score, int day) => new(Guid.NewGuid(), Day.AddDays(day), score);
}
