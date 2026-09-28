using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Sessions;

public sealed class AttemptTests
{
    private readonly SessionBuilder _builder = new();

    [Theory]
    [InlineData(1, GradeOutcome.Correct)]
    [InlineData(0.5, GradeOutcome.Partial)]
    [InlineData(0, GradeOutcome.Incorrect)]
    public void Outcome_ByNormalisedScore_MapsToGradeOutcome(double normalised, GradeOutcome expected)
    {
        var session = _builder.Build();

        var attempt = session.RecordAttempt(session.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade((decimal)normalised), 0);

        attempt.Outcome.Should().Be(expected);
    }

    [Fact]
    public void ReadFeedback_NoFeedback_ReturnsNull()
    {
        var session = _builder.Build();

        var attempt = session.RecordAttempt(session.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);

        attempt.Grade.Should().BeNull();
        attempt.ReadFeedback().Should().BeNull();
    }
}
