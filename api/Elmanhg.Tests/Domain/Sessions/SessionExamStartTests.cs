using Core.Errors;
using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Domain.Units;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using System.Text.Json;

namespace Elmanhg.Tests.Domain.Sessions;

public sealed class SessionExamStartTests
{
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(30);
    private readonly ExamSessionBuilder _builder = new();

    [Fact]
    public void StartUnitExam_ServableQuestions_CreatesUnitExamWithBlueprintSettings()
    {
        var questions = _builder.BuildQuestions(3);

        var session = Start(_builder.Blueprint(3), questions);

        session.Kind.Should().Be(SessionKind.UnitExam);
        session.IsExam.Should().BeTrue();
        JsonDocument.Parse(session.Scope).RootElement.GetProperty("unitId").GetGuid().Should().Be(_builder.Questions.Unit.Id);
        session.ScopeKey.Should().Be($"unit:{_builder.Questions.Unit.Id:D}");
        session.TimeLimitMinutes.Should().Be(30);
        session.PassMark.Should().Be(50);
        session.Deadline.Should().Be(session.StartedAt.AddMinutes(30));
        session.Items.Select(x => (x.Position, x.QuestionId)).Should().Equal(questions.Select((x, index) => (index + 1, x.Id)));
    }

    [Fact]
    public void StartUnitExam_UntimedBlueprint_HasNoDeadline()
    {
        var session = _builder.Build(timeLimitMinutes: null);

        session.Deadline.Should().BeNull();
        session.TimeLimitMinutes.Should().BeNull();
    }

    [Fact]
    public void StartUnitExam_SubjectDefaultBlueprint_IsAccepted()
    {
        var blueprint = ExamBlueprint.CreateForSubject(_builder.Questions.Subject, new ExamBlueprintShape([new ExamTypeCount(QuestionType.Mcq, 1)], null, null, 70), ExamBlueprintBuilder.Plenty(), Guid.NewGuid());

        var session = Start(blueprint, _builder.BuildQuestions(1));

        session.PassMark.Should().Be(70);
    }

    [Fact]
    public void StartUnitExam_BlueprintOfOtherUnit_ThrowsInvalidOperation()
    {
        var otherUnit = CurriculumUnit.Create(_builder.Questions.Subject, "Optics", 2, Guid.NewGuid());
        var blueprint = ExamBlueprint.CreateForUnit(otherUnit, new ExamBlueprintShape([new ExamTypeCount(QuestionType.Mcq, 1)], null, 30, 50), ExamBlueprintBuilder.Plenty(), Guid.NewGuid());

        var act = () => Start(blueprint, _builder.BuildQuestions(1));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void StartUnitExam_NoQuestions_ThrowsNoServableQuestions()
    {
        var act = () => Start(_builder.Blueprint(1), []);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.SessionNoServableQuestions);
    }

    [Fact]
    public void StartUnitExam_QuestionInUnpublishedLesson_ThrowsNotServable()
    {
        var draft = new QuestionBuilder();
        var question = draft.Approved().Build();

        var act = () => Session.StartUnitExam(_builder.StudentId, draft.Unit, ExamBlueprint.CreateForUnit(draft.Unit, new ExamBlueprintShape([new ExamTypeCount(QuestionType.Mcq, 1)], null, 30, 50), ExamBlueprintBuilder.Plenty(), Guid.NewGuid()), [question], [draft.Lesson], false, ExamSessionBuilder.Now);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.SessionQuestionNotServable);
    }

    [Fact]
    public void StartUnitExam_LessonOfOtherUnit_ThrowsNotServable()
    {
        var other = new QuestionBuilder();
        other.Lesson.Publish(Guid.NewGuid());
        var question = other.Approved().Build();

        var act = () => Session.StartUnitExam(_builder.StudentId, _builder.Questions.Unit, _builder.Blueprint(1), [question], [other.Lesson], false, ExamSessionBuilder.Now);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.SessionQuestionNotServable);
    }

    [Fact]
    public void StartUnitExam_DuplicateQuestion_ThrowsDuplicate()
    {
        var question = _builder.BuildQuestions(1)[0];

        var act = () => Start(_builder.Blueprint(2), [question, question]);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.SessionQuestionDuplicate);
    }

    [Fact]
    public void StartUnitExam_Now_TruncatesToMicroseconds()
    {
        var now = ExamSessionBuilder.Now.AddTicks(7);

        var session = Session.StartUnitExam(_builder.StudentId, _builder.Questions.Unit, _builder.Blueprint(1), _builder.BuildQuestions(1), [_builder.Questions.Lesson], false, now);

        (session.StartedAt.Ticks % TimeSpan.TicksPerMicrosecond).Should().Be(0);
        session.StartedAt.Should().Be(ExamSessionBuilder.Now);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(10, false)]
    [InlineData(31, true)]
    public void IsPastDeadline_RelativeToDeadlineAndGrace_ReturnsExpected(int secondsAfterDeadline, bool expected)
    {
        var session = _builder.Build();

        var result = session.IsPastDeadline(session.Deadline.GetValueOrDefault().AddSeconds(secondsAfterDeadline), Grace);

        result.Should().Be(expected);
    }

    [Fact]
    public void IsPastDeadline_Untimed_ReturnsFalse()
    {
        var session = _builder.Build(timeLimitMinutes: null);

        session.IsPastDeadline(ExamSessionBuilder.Now.AddYears(1), Grace).Should().BeFalse();
    }

    private Session Start(ExamBlueprint blueprint, IReadOnlyList<Question> questions) => Session.StartUnitExam(_builder.StudentId, _builder.Questions.Unit, blueprint, questions, [_builder.Questions.Lesson], false, ExamSessionBuilder.Now);
}
