using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.TrainingData;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.TrainingData;

public sealed class EssayGradeTrainingRecordTests
{
    private const string StudentHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly DateTimeOffset GradedAt = EssayGradeBuilder.DefaultRequestedAt.AddSeconds(40);
    private static readonly DateTimeOffset RecordedAt = GradedAt.AddSeconds(1);

    [Fact]
    public void From_CompletedGrade_CopiesGradeAndPlacement()
    {
        var grade = CompletedGrade();
        var placement = new QuestionPlacement(grade.QuestionId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var record = EssayGradeTrainingRecord.From(grade, SessionKind.UnitExam, placement, StudentHash, RecordedAt);

        (record.StudentHash, record.EssayGradeId, record.QuestionId, record.QuestionVersion, record.SessionKind).Should().Be((StudentHash, grade.Id, grade.QuestionId, grade.QuestionVersion, SessionKind.UnitExam));
        (record.SubjectId, record.UnitId, record.LessonId).Should().Be((placement.SubjectId, placement.UnitId, placement.LessonId));
        (record.Answer, record.MaxScore, record.Score, record.NormalisedScore, record.Criteria).Should().Be((grade.Answer, 5, 2.5m, 0.5m, grade.Criteria!));
        (record.Justification, record.Confidence, record.Outcome, record.Model, record.PromptVersion).Should().Be(("جيد", 0.5m, EssayGradeStatus.InReview, "claude-sonnet-5", "v1"));
        (record.OccurredAt, record.RecordedAt).Should().Be((GradedAt, RecordedAt));
        record.Id.Should().NotBe(grade.Id);
    }

    [Fact]
    public void From_PendingGrade_ThrowsInvalidOperationException()
    {
        var grade = new EssayGradeBuilder().Build();
        var placement = new QuestionPlacement(grade.QuestionId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var act = () => EssayGradeTrainingRecord.From(grade, SessionKind.Quiz, placement, StudentHash, RecordedAt);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void From_PlacementOfOtherQuestion_ThrowsInvalidOperationException()
    {
        var grade = CompletedGrade();
        var placement = new QuestionPlacement(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var act = () => EssayGradeTrainingRecord.From(grade, SessionKind.Quiz, placement, StudentHash, RecordedAt);

        act.Should().Throw<InvalidOperationException>();
    }

    private static EssayGrade CompletedGrade()
    {
        var grade = new EssayGradeBuilder().Build();
        grade.Complete(EssayGradeBuilder.Assessment(0.5m), new QuestionGrade(2.5m, 0.5m, GradeOutcome.Partial, null), 0.7m, GradedAt);
        return grade;
    }
}
