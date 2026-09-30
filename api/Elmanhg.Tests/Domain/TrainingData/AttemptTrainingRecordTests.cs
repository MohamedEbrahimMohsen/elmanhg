using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.TrainingData;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.TrainingData;

public sealed class AttemptTrainingRecordTests
{
    private const string StudentHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly DateTimeOffset RecordedAt = new(2026, 10, 2, 8, 0, 0, TimeSpan.Zero);
    private readonly SessionBuilder _builder = new();

    [Fact]
    public void From_Attempt_CopiesAnswerScoreGradeAndPlacement()
    {
        var attempt = RecordAttempt();
        var placement = new QuestionPlacement(attempt.QuestionId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var record = AttemptTrainingRecord.From(attempt, SessionKind.Quiz, placement, StudentHash, RecordedAt);

        (record.StudentHash, record.AttemptId, record.QuestionId, record.QuestionVersion).Should().Be((StudentHash, attempt.Id, attempt.QuestionId, attempt.QuestionVersion));
        (record.SubjectId, record.UnitId, record.LessonId).Should().Be((placement.SubjectId, placement.UnitId, placement.LessonId));
        (record.SessionKind, record.Answer, record.Score, record.NormalisedScore).Should().Be((SessionKind.Quiz, SessionBuilder.AnswerB, attempt.Score, 0.5m));
        (record.GradedBy, record.Grade, record.TimeTakenMilliseconds).Should().Be((attempt.GradedBy, attempt.Grade, attempt.TimeTakenMilliseconds));
        record.Grade.Should().NotBeNull();
        (record.OccurredAt, record.RecordedAt).Should().Be((attempt.CreatedAt, RecordedAt));
        record.Id.Should().NotBe(attempt.Id);
    }

    [Fact]
    public void From_PlacementOfOtherQuestion_ThrowsInvalidOperationException()
    {
        var attempt = RecordAttempt();
        var placement = new QuestionPlacement(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var act = () => AttemptTrainingRecord.From(attempt, SessionKind.Quiz, placement, StudentHash, RecordedAt);

        act.Should().Throw<InvalidOperationException>().WithMessage("Placement does not belong to the attempt's question.");
    }

    [Fact]
    public void From_BlankStudentHash_ThrowsArgumentException()
    {
        var attempt = RecordAttempt();
        var placement = new QuestionPlacement(attempt.QuestionId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var act = () => AttemptTrainingRecord.From(attempt, SessionKind.Quiz, placement, " ", RecordedAt);

        act.Should().Throw<ArgumentException>();
    }

    private Attempt RecordAttempt()
    {
        var session = _builder.Build();
        return session.RecordAttempt(session.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(0.5m, GradeFeedback.ChoiceTally(1, 1, 2)), 0);
    }
}
