using Elmanhg.Domain.Mastery;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Mastery;

public sealed class MasteryAttemptTests
{
    [Fact]
    public void From_Attempt_CopiesIdScoreAndTime()
    {
        var session = new SessionBuilder().Build();
        var attempt = session.RecordAttempt(session.Items[0], SessionBuilder.AnswerA, SessionBuilder.Grade(0.5m), 0);

        var result = MasteryAttempt.From(attempt);

        result.Should().Be(new MasteryAttempt(attempt.Id, 0.5m, attempt.CreatedAt));
    }
}
