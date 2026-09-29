using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.TeacherThreads.Shared;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.TeacherThreads.Shared;

public sealed class TeacherThreadContextResolverTests
{
    private readonly ILessonRepository _lessons = Substitute.For<ILessonRepository>();
    private readonly ICurriculumUnitRepository _units = Substitute.For<ICurriculumUnitRepository>();
    private readonly ISubjectRepository _subjects = Substitute.For<ISubjectRepository>();
    private readonly IQuestionRepository _questions = Substitute.For<IQuestionRepository>();
    private readonly ISessionRepository _sessions = Substitute.For<ISessionRepository>();
    private readonly SessionBuilder _builder = new();
    private readonly Guid _studentId;

    public TeacherThreadContextResolverTests()
    {
        _studentId = _builder.StudentId;
        var content = _builder.Questions;
        _lessons.GetByIdAsync(content.Lesson.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<bool>()).Returns(content.Lesson);
        _units.GetByIdAsync(content.Unit.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<bool>()).Returns(content.Unit);
        _subjects.GetByIdAsync(content.Subject.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<bool>()).Returns(content.Subject);
    }

    private QuestionBuilder Content => _builder.Questions;

    [Fact]
    public async Task ResolveAsync_PublishedLesson_ReturnsSubjectUnitLessonWithoutQuestion()
    {
        var context = await Resolve(lessonId: Content.Lesson.Id);

        (context.SubjectId, context.SubjectName, context.UnitId, context.UnitName, context.LessonId, context.LessonName).Should().Be((Content.Subject.Id, "Physics", Content.Unit.Id, "Mechanics", Content.Lesson.Id, "Newton's laws"));
        (context.QuestionId, context.QuestionVersion, context.QuestionStem, context.AttemptId).Should().Be(((Guid?)null, (int?)null, (string?)null, (Guid?)null));
    }

    [Fact]
    public async Task ResolveAsync_UnknownLesson_ThrowsLessonNotFound()
    {
        var act = () => Resolve(lessonId: Guid.NewGuid());

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonNotFound);
    }

    [Fact]
    public async Task ResolveAsync_DraftLesson_ThrowsLessonNotFound()
    {
        Content.Lesson.Unpublish(Guid.NewGuid());

        var act = () => Resolve(lessonId: Content.Lesson.Id);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonNotFound);
    }

    [Fact]
    public async Task ResolveAsync_ServableQuestion_ReturnsCurrentStemAndVersion()
    {
        var question = StubQuestion(Content.Approved().Build());

        var context = await Resolve(questionId: question.Id);

        (context.QuestionId, context.QuestionVersion, context.QuestionStem, context.AttemptId).Should().Be(((Guid?)question.Id, (int?)question.Version, question.Stem, (Guid?)null));
    }

    [Fact]
    public async Task ResolveAsync_UnknownQuestion_ThrowsQuestionNotFound()
    {
        var act = () => Resolve(questionId: Guid.NewGuid());

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.QuestionNotFound);
    }

    [Fact]
    public async Task ResolveAsync_PendingQuestion_ThrowsQuestionNotFound()
    {
        var question = StubQuestion(Content.Build());

        var act = () => Resolve(questionId: question.Id);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.QuestionNotFound);
    }

    [Fact]
    public async Task ResolveAsync_OwnAttempt_ReturnsServedRevisionStemAndAttemptId()
    {
        var (attempt, question) = AnsweredAttempt();
        var servedStem = question.Stem;
        question.Update(QuestionType.Mcq, QuestionBuilder.McqContent() with { Stem = "<p>New stem</p>" }, new QuestionMetadata(QuestionDifficulty.Medium, null, []), Content.Lesson, Guid.NewGuid());
        _questions.GetRevisionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(question.Revisions);

        var context = await Resolve(attemptId: attempt.Id);

        (question.Version, context.QuestionId, context.QuestionVersion, context.QuestionStem, context.AttemptId).Should().Be((2, (Guid?)question.Id, (int?)1, servedStem, (Guid?)attempt.Id));
    }

    [Fact]
    public async Task ResolveAsync_AttemptNotOwned_ThrowsAttemptNotFound()
    {
        var act = () => Resolve(attemptId: Guid.NewGuid());

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.AttemptNotFound);
    }

    [Fact]
    public async Task ResolveAsync_AttemptFromOpenExam_ThrowsTeacherThreadExamInProgress()
    {
        var (attempt, question) = AnsweredAttempt();
        _questions.GetRevisionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(question.Revisions);
        var openExam = new ExamSessionBuilder().Build(1);
        _sessions.GetByIdAsync(attempt.SessionId, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Session>, IQueryable<Session>>?>(), Arg.Any<bool>()).Returns(openExam);

        var act = () => Resolve(attemptId: attempt.Id);

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.TeacherThreadExamInProgress);
        openExam.IsSubmitted.Should().BeFalse();
    }

    [Fact]
    public async Task ResolveAsync_AttemptSessionMissing_ThrowsAttemptNotFound()
    {
        var (attempt, _) = AnsweredAttempt();
        _sessions.GetByIdAsync(attempt.SessionId, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Session>, IQueryable<Session>>?>(), Arg.Any<bool>()).Returns((Session?)null);

        var act = () => Resolve(attemptId: attempt.Id);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.AttemptNotFound);
    }

    [Fact]
    public async Task ResolveAsync_AttemptRevisionMissing_ThrowsQuestionNotFound()
    {
        var (attempt, _) = AnsweredAttempt();
        _questions.GetRevisionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([]);

        var act = () => Resolve(attemptId: attempt.Id);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.QuestionNotFound);
    }

    [Fact]
    public async Task ResolveAsync_AttemptOnUnpublishedLesson_ThrowsLessonNotFound()
    {
        var (attempt, question) = AnsweredAttempt();
        _questions.GetRevisionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(question.Revisions);
        Content.Lesson.Unpublish(Guid.NewGuid());

        var act = () => Resolve(attemptId: attempt.Id);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonNotFound);
    }

    private (Attempt Attempt, Question Question) AnsweredAttempt()
    {
        var question = StubQuestion(Content.Approved().Build());
        var session = Session.StartQuiz(_studentId, Content.Lesson, [question], false);
        var attempt = session.RecordAttempt(session.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);
        _sessions.GetStudentAttemptAsync(attempt.Id, _studentId, Arg.Any<CancellationToken>()).Returns(attempt);
        StubSession(session);
        return (attempt, question);
    }

    private void StubSession(Session session) => _sessions.GetByIdAsync(session.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Session>, IQueryable<Session>>?>(), Arg.Any<bool>()).Returns(session);

    private Question StubQuestion(Question question)
    {
        _questions.GetByIdAsync(question.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Question>, IQueryable<Question>>?>(), Arg.Any<bool>()).Returns(question);
        return question;
    }

    private Task<Elmanhg.Domain.TeacherThreads.TeacherThreadContext> Resolve(Guid? lessonId = null, Guid? questionId = null, Guid? attemptId = null) => TeacherThreadContextResolver.ResolveAsync(lessonId, questionId, attemptId, _studentId, new TeacherThreadContextSources(_lessons, _units, _subjects, _questions, _sessions), TestContext.Current.CancellationToken);
}
