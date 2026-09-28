using Core.Errors;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using System.Text.Json.Nodes;

namespace Elmanhg.Tests.Domain.Sessions;

public sealed class SessionTests
{
    private readonly SessionBuilder _builder = new();

    [Fact]
    public void StartQuiz_ServableQuestions_CreatesOrderedItemsAtServedVersion()
    {
        var questions = _builder.BuildQuestions(2);

        var session = Session.StartQuiz(_builder.StudentId, _builder.Questions.Lesson, questions, isTestMode: false);

        session.Kind.Should().Be(SessionKind.Quiz);
        session.Items.Select(x => (x.Position, x.QuestionId)).Should().Equal((1, questions[0].Id), (2, questions[1].Id));
        session.Items.Should().AllSatisfy(item =>
        {
            item.QuestionVersion.Should().Be(questions.Single(x => x.Id == item.QuestionId).Version);
            item.MaxScore.Should().Be(1);
            item.SessionId.Should().Be(session.Id);
        });
        (session.StudentId, session.CreatedBy).Should().Be((_builder.StudentId, (Guid?)_builder.StudentId));
        session.IsTestMode.Should().BeFalse();
        session.SubmittedAt.Should().BeNull();
        session.LastActivityAt.Should().Be(session.StartedAt);
    }

    [Fact]
    public void StartQuiz_Scope_StoresLessonJsonAndKey()
    {
        var lessonId = _builder.Questions.Lesson.Id;

        var session = _builder.Build();

        session.ScopeKey.Should().Be($"lesson:{lessonId:D}");
        JsonNode.DeepEquals(JsonNode.Parse(session.Scope), JsonNode.Parse($$"""{"lessonId":"{{lessonId}}"}""")).Should().BeTrue();
    }

    [Fact]
    public void StartQuiz_TestMode_FlagsSession()
    {
        var session = _builder.Build(isTestMode: true);

        session.IsTestMode.Should().BeTrue();
    }

    [Fact]
    public void StartQuiz_NoQuestions_ThrowsSessionNoServableQuestions()
    {
        var act = () => Session.StartQuiz(_builder.StudentId, _builder.Questions.Lesson, [], isTestMode: false);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.SessionNoServableQuestions);
    }

    [Fact]
    public void StartQuiz_PendingQuestion_ThrowsSessionQuestionNotServable()
    {
        var pending = _builder.Questions.Build();

        var act = () => Session.StartQuiz(_builder.StudentId, _builder.Questions.Lesson, [pending], isTestMode: false);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.SessionQuestionNotServable);
    }

    [Fact]
    public void StartQuiz_LessonNotPublished_ThrowsSessionQuestionNotServable()
    {
        var draftLesson = new QuestionBuilder();

        var act = () => Session.StartQuiz(_builder.StudentId, draftLesson.Lesson, [draftLesson.Approved().Build()], isTestMode: false);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.SessionQuestionNotServable);
    }

    [Fact]
    public void StartQuiz_RetiredQuestion_ThrowsSessionQuestionNotServable()
    {
        var retired = _builder.Questions.Approved().Retired().Build();

        var act = () => Session.StartQuiz(_builder.StudentId, _builder.Questions.Lesson, [retired], isTestMode: false);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.SessionQuestionNotServable);
    }

    [Fact]
    public void StartQuiz_SameQuestionTwice_ThrowsSessionQuestionDuplicate()
    {
        var question = _builder.BuildQuestions(1)[0];

        var act = () => Session.StartQuiz(_builder.StudentId, _builder.Questions.Lesson, [question, question], isTestMode: false);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.SessionQuestionDuplicate);
    }

    [Fact]
    public void Resume_InProgress_MovesLastActivityForward()
    {
        var session = _builder.Build();
        var before = session.LastActivityAt;

        session.Resume();

        session.LastActivityAt.Should().BeOnOrAfter(before);
        session.UpdationDate.Should().Be(session.LastActivityAt);
        session.UpdatedBy.Should().Be(_builder.StudentId);
    }

    [Fact]
    public void Resume_Submitted_ThrowsSessionAlreadySubmitted()
    {
        var session = _builder.Build();
        session.Submit();

        var act = session.Resume;

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.SessionAlreadySubmitted);
    }

    [Fact]
    public void CurrentPosition_NoAttempts_IsFirst()
    {
        _builder.Build().CurrentPosition.Should().Be(1);
    }

    [Fact]
    public void CurrentPosition_FirstAnswered_IsSecond()
    {
        var session = _builder.Build();

        session.RecordAttempt(session.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);

        session.CurrentPosition.Should().Be(2);
    }

    [Fact]
    public void CurrentPosition_AllAnswered_IsNull()
    {
        var session = _builder.Build();

        session.Items.ForEach(item => session.RecordAttempt(item, SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0));

        session.CurrentPosition.Should().BeNull();
    }

    [Fact]
    public void CurrentPosition_Submitted_IsNull()
    {
        var session = _builder.Build();

        session.Submit();

        session.CurrentPosition.Should().BeNull();
        session.Attempts.Should().BeEmpty();
    }
}
