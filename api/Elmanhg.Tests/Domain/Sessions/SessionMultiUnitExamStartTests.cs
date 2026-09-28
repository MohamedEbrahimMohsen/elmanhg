using Core.Errors;
using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Sessions.Exams;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Domain.Units;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Sessions;

public sealed class SessionMultiUnitExamStartTests
{
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(30);
    private readonly MultiUnitExamBuilder _builder = new();

    [Fact]
    public void StartMultiUnitExam_SetsKindScopeDeadlineAndPassMark()
    {
        var session = _builder.Build();

        session.Kind.Should().Be(SessionKind.MultiUnitExam);
        session.ScopeKey.Should().Be(new MultiUnitExamScope(_builder.Subject.Id, _builder.Units.Select(x => x.Id).ToList(), 20).ToKey());
        session.Deadline.Should().Be(session.StartedAt.AddMinutes(60));
        session.PassMark.Should().Be(50);
        session.Items.Select(x => x.Position).Should().Equal(Enumerable.Range(1, 20));
    }

    [Fact]
    public void StartMultiUnitExam_UntimedPlan_HasNoDeadline()
    {
        var session = _builder.Build(timeLimitMinutes: null);

        session.Deadline.Should().BeNull();
    }

    [Fact]
    public void StartMultiUnitExam_QuestionOutsideSelectedUnits_ThrowsNotServable()
    {
        var other = new MultiUnitExamBuilder();

        var act = () => Start(_builder.Units, [_builder.Approved(0), other.Approved(0)], [.. _builder.Lessons, .. other.Lessons]);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.SessionQuestionNotServable);
    }

    [Fact]
    public void StartMultiUnitExam_NoQuestions_ThrowsNoServableQuestions()
    {
        var act = () => Start(_builder.Units, [], _builder.Lessons);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.SessionNoServableQuestions);
    }

    [Fact]
    public void StartMultiUnitExam_DuplicateQuestion_ThrowsDuplicate()
    {
        var question = _builder.Approved(1);

        var act = () => Start(_builder.Units, [question, question], _builder.Lessons);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.SessionQuestionDuplicate);
    }

    [Fact]
    public void StartMultiUnitExam_UnitOfOtherSubject_ThrowsInvalidOperation()
    {
        var other = new MultiUnitExamBuilder();

        var act = () => Start([_builder.Units[0], other.Units[0]], [_builder.Approved(0)], _builder.Lessons);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void StartMultiUnitExam_OneUnit_ThrowsInvalidOperation()
    {
        var act = () => Start([_builder.Units[0]], [_builder.Approved(0)], _builder.Lessons);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GetExamUnitIds_MultiUnitExam_ReturnsScopeOrder()
    {
        var session = _builder.Build();

        session.GetExamUnitIds().Should().Equal(_builder.Units[0].Id, _builder.Units[1].Id);
    }

    [Fact]
    public void GetExamUnitIds_UnitExam_ReturnsItsUnit()
    {
        var unitExam = new ExamSessionBuilder();

        unitExam.Build().GetExamUnitIds().Should().Equal(unitExam.Questions.Unit.Id);
    }

    [Fact]
    public void SubmitExam_MultiUnitExam_ScoresLikeUnitExam()
    {
        var session = _builder.Build(perUnit: 1);
        session.SaveExamAnswer(session.Items[0], SessionBuilder.AnswerB, Grace, MultiUnitExamBuilder.Now.AddMinutes(1));
        session.SaveExamAnswer(session.Items[1], SessionBuilder.AnswerB, Grace, MultiUnitExamBuilder.Now.AddMinutes(1));

        session.SubmitExam(new Dictionary<Guid, QuestionGrade> { [session.Items[0].QuestionId] = SessionBuilder.Grade(1m), [session.Items[1].QuestionId] = SessionBuilder.Grade(0m) }, MultiUnitExamBuilder.Now.AddMinutes(2));

        session.ScorePercent.Should().Be(50m);
    }

    private Session Start(IReadOnlyList<CurriculumUnit> units, IReadOnlyList<Question> questions, IReadOnlyCollection<Lesson> lessons)
    {
        var parts = _builder.Units
            .Select((unit, index) => new MultiUnitExamPart(unit.Id, _builder.UnitBlueprint(index, 30, 50, new ExamTypeCount(QuestionType.Mcq, 10))))
            .ToList();
        var plan = MultiUnitBlueprintMerge.Merge(parts, 20, MultiUnitExamBuilder.MaxTimeLimitMinutes);
        return Session.StartMultiUnitExam(_builder.StudentId, _builder.Subject.Id, units, plan, 20, questions, lessons, false, MultiUnitExamBuilder.Now);
    }
}
