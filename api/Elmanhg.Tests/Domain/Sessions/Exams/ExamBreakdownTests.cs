using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Sessions.Exams;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Sessions.Exams;

public sealed class ExamBreakdownTests
{
    private const decimal Threshold = 0.8m;
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(30);
    private readonly ExamSessionBuilder _builder = new();
    private readonly Guid _lessonA = Guid.NewGuid();
    private readonly Guid _lessonB = Guid.NewGuid();

    [Fact]
    public void ByLesson_SubmittedExam_SumsScoresPerLesson()
    {
        var session = Submitted(3, 1m, 0m, 1m);
        List<ExamItemPlacement> placements = [Place(session, 0, _lessonA, 1), Place(session, 1, _lessonA, 1), Place(session, 2, _lessonB, 2)];

        var shares = ExamBreakdown.ByLesson(session, placements, Threshold);

        shares.Select(x => (x.Id, x.Score, x.MaxScore, x.ScorePercent)).Should().Equal((_lessonA, 1m, 2, 50.00m), (_lessonB, 1m, 1, 100.00m));
    }

    [Fact]
    public void ByLesson_UnansweredItems_CountZero()
    {
        var session = Submitted(2, 1m);

        var share = ExamBreakdown.ByLesson(session, [Place(session, 0, _lessonA, 1), Place(session, 1, _lessonA, 1)], Threshold).Single();

        (share.QuestionCount, share.Score, share.MaxScore).Should().Be((2, 1m, 2));
    }

    [Fact]
    public void ByLesson_CorrectCount_UsesThreshold()
    {
        var session = Submitted(2, 1m, 0.5m);

        var share = ExamBreakdown.ByLesson(session, [Place(session, 0, _lessonA, 1), Place(session, 1, _lessonA, 1)], Threshold).Single();

        share.CorrectCount.Should().Be(1);
    }

    [Fact]
    public void ByLesson_OrdersByLessonOrder()
    {
        var session = Submitted(2, 1m, 1m);

        var shares = ExamBreakdown.ByLesson(session, [Place(session, 0, _lessonA, 2), Place(session, 1, _lessonB, 1)], Threshold);

        shares.Select(x => x.Id).Should().Equal(_lessonB, _lessonA);
    }

    [Fact]
    public void ByLesson_OpenExam_ReturnsEmpty()
    {
        var session = _builder.Build();

        ExamBreakdown.ByLesson(session, [Place(session, 0, _lessonA, 1)], Threshold).Should().BeEmpty();
    }

    [Fact]
    public void ByLesson_ItemWithoutPlacement_IsSkipped()
    {
        var session = Submitted(2, 1m, 1m);

        var shares = ExamBreakdown.ByLesson(session, [Place(session, 0, _lessonA, 1)], Threshold);

        shares.Should().ContainSingle().Which.QuestionCount.Should().Be(1);
    }

    [Fact]
    public void WeakestObjectives_ExcludesFullScoreAndSortsAscending()
    {
        var session = Submitted(4, 1m, 0m, 1m, 0m);
        Guid full = Guid.NewGuid(), zero = Guid.NewGuid(), half = Guid.NewGuid();
        List<ExamItemPlacement> placements = [Place(session, 0, _lessonA, 1, full, 1), Place(session, 1, _lessonA, 1, zero, 2), Place(session, 2, _lessonA, 1, half, 3), Place(session, 3, _lessonA, 1, half, 3)];

        var shares = ExamBreakdown.WeakestObjectives(session, placements, Threshold, 3);

        shares.Select(x => (x.Id, x.ScorePercent)).Should().Equal((zero, 0m), (half, 50.00m));
    }

    [Fact]
    public void WeakestObjectives_TiesBrokenByLessonThenObjectiveOrder()
    {
        var session = Submitted(3, 0m, 0m, 0m);
        Guid secondLesson = Guid.NewGuid(), firstLessonSecond = Guid.NewGuid(), firstLessonFirst = Guid.NewGuid();
        List<ExamItemPlacement> placements = [Place(session, 0, _lessonB, 2, secondLesson, 1), Place(session, 1, _lessonA, 1, firstLessonSecond, 2), Place(session, 2, _lessonA, 1, firstLessonFirst, 1)];

        var shares = ExamBreakdown.WeakestObjectives(session, placements, Threshold, 3);

        shares.Select(x => x.Id).Should().Equal(firstLessonFirst, firstLessonSecond, secondLesson);
    }

    [Fact]
    public void WeakestObjectives_TakesCount()
    {
        var session = Submitted(4, 0m, 0m, 0m, 0m);
        var placements = Enumerable.Range(0, 4).Select(index => Place(session, index, _lessonA, 1, Guid.NewGuid(), index + 1)).ToList();

        var shares = ExamBreakdown.WeakestObjectives(session, placements, Threshold, 3);

        shares.Select(x => x.ObjectiveOrder).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void WeakestObjectives_IgnoresItemsWithoutObjective()
    {
        var session = Submitted(1, 0m);

        ExamBreakdown.WeakestObjectives(session, [Place(session, 0, _lessonA, 1)], Threshold, 3).Should().BeEmpty();
    }

    private Session Submitted(int count, params decimal[] answeredScores)
    {
        var session = _builder.Build(count);
        Dictionary<Guid, QuestionGrade> grades = [];
        for (var index = 0; index < answeredScores.Length; index++)
        {
            session.SaveExamAnswer(session.Items[index], SessionBuilder.AnswerB, Grace, ExamSessionBuilder.Now.AddMinutes(1));
            grades[session.Items[index].QuestionId] = SessionBuilder.Grade(answeredScores[index]);
        }

        session.SubmitExam(grades, ExamSessionBuilder.Now.AddMinutes(2));
        return session;
    }

    private static ExamItemPlacement Place(Session session, int index, Guid lessonId, int lessonOrder, Guid? objectiveId = null, int objectiveOrder = 0) => new(session.Items[index].QuestionId, lessonId, lessonOrder, objectiveId, objectiveOrder);
}
