using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Localization;
using Elmanhg.Application.Exams.GetExamSession;
using Elmanhg.Application.Exams.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Sessions.Exams;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using Elmanhg.Tests.Application.Features.Sessions;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Exams.GetExamSession;

public sealed class GetExamSessionHandlerTests
{
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(30);
    private static readonly DateTimeOffset Clock = ExamSessionBuilder.Now.AddMinutes(5);
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly ExamSessionBuilder _builder = new();
    private readonly List<Question> _questions;
    private readonly Session _session;
    private readonly GetExamSessionHandler _handler;

    public GetExamSessionHandlerTests()
    {
        _questions = _builder.BuildQuestions(2);
        _session = Session.StartUnitExam(_builder.StudentId, _builder.Questions.Unit, _builder.Blueprint(2), _questions, [_builder.Questions.Lesson], false, ExamSessionBuilder.Now);
        _session.SaveExamAnswer(_session.Items[0], SessionBuilder.AnswerB, Grace, ExamSessionBuilder.Now.AddMinutes(1));
        _currentUserService.UserId.Returns(_builder.StudentId);
        _timeProvider.GetUtcNow().Returns(Clock);
        SessionRepositoryStub.StubFind(_sessionRepository, _session);
        _questionRepository.GetRevisionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(_questions.SelectMany(x => x.Revisions).ToList());
        _questionRepository.FindAsync(Arg.Any<Expression<Func<Question, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Question>, IQueryable<Question>>?>(), Arg.Any<Func<IQueryable<Question>, IOrderedQueryable<Question>>?>(), Arg.Any<bool>())
            .Returns(call => _questions.Where(call.Arg<Expression<Func<Question, bool>>>().Compile()).ToList());
        _lessonRepository.FindAsync(Arg.Any<Expression<Func<Lesson, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<Func<IQueryable<Lesson>, IOrderedQueryable<Lesson>>?>(), Arg.Any<bool>())
            .Returns([_builder.Questions.Lesson]);
        _unitRepository.GetByIdAsync(_builder.Questions.Unit.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<bool>()).Returns(_builder.Questions.Unit);
        _subjectRepository.GetByIdAsync(_builder.Questions.Subject.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<bool>()).Returns(_builder.Questions.Subject);
        _handler = new GetExamSessionHandler(_sessionRepository, _questionRepository, _lessonRepository, _unitRepository, _subjectRepository, Options.Create(new ExamsOptions()), Options.Create(new MasteryOptions()), _timeProvider, _currentUserService, Substitute.For<ILocalizer>());
    }

    [Fact]
    public async Task Handle_NoUser_ThrowsUnauthorized()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        await AssertThrowsAsync<UnauthorizedCoreException>(_session.Id, ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_UnknownSession_ThrowsNotFound()
    {
        await AssertThrowsAsync<NotFoundCoreException>(Guid.NewGuid(), ErrorCodes.SessionNotFound);
    }

    [Fact]
    public async Task Handle_QuizSession_ThrowsNotFound()
    {
        var quiz = Session.StartQuiz(_builder.StudentId, _builder.Questions.Lesson, _questions, isTestMode: false);
        SessionRepositoryStub.StubFind(_sessionRepository, quiz);

        await AssertThrowsAsync<NotFoundCoreException>(quiz.Id, ErrorCodes.SessionNotFound);
    }

    [Fact]
    public async Task Handle_OpenExam_ReturnsSavedAnswersWithoutKeys()
    {
        var result = await _handler.Handle(new GetExamSessionQuery(_session.Id), TestContext.Current.CancellationToken);

        result.Items[0].SavedAnswer!.Value.GetProperty("optionId").GetString().Should().Be("b");
        result.Items[0].AnswerSavedAt.Should().Be(ExamSessionBuilder.Now.AddMinutes(1));
        result.Items.Should().AllSatisfy(x =>
        {
            x.Attempt.Should().BeNull();
            x.CorrectAnswer.Should().BeNull();
            x.Explanation.Should().BeNull();
        });
        result.ServerNow.Should().Be(Clock);
        result.Lessons.Should().BeEmpty();
        result.SubjectName.Should().Be(_builder.Questions.Subject.Name);
    }

    [Fact]
    public async Task Handle_SubmittedExam_ReturnsBreakdown()
    {
        _session.SubmitExam(new Dictionary<Guid, QuestionGrade> { [_session.Items[0].QuestionId] = SessionBuilder.Grade(1m) }, ExamSessionBuilder.Now.AddMinutes(2));

        var result = await _handler.Handle(new GetExamSessionQuery(_session.Id), TestContext.Current.CancellationToken);

        result.IsPassed.Should().BeTrue();
        result.ScorePercent.Should().Be(50.00m);
        result.ElapsedMilliseconds.Should().Be(120_000);
        result.Lessons.Should().ContainSingle().Which.Should().Match<ExamLessonResult>(x => x.LessonId == _builder.Questions.Lesson.Id && x.ScorePercent == 50.00m && x.Name == _builder.Questions.Lesson.Name);
        result.Items.Should().AllSatisfy(x => x.CorrectAnswer.Should().NotBeNull());
    }

    [Fact]
    public async Task Handle_SubmittedMultiUnitExam_ReturnsUnitsSubjectAndUnitBreakdown()
    {
        var multi = new MultiUnitExamBuilder();
        List<Question> questions = [multi.Approved(1), multi.Approved(0)];
        var plan = MultiUnitBlueprintMerge.Merge([new MultiUnitExamPart(multi.Units[0].Id, multi.UnitBlueprint(0, 30, 50, new ExamTypeCount(QuestionType.Mcq, 1))), new MultiUnitExamPart(multi.Units[1].Id, multi.UnitBlueprint(1, 30, 50, new ExamTypeCount(QuestionType.Mcq, 1)))], 20, MultiUnitExamBuilder.MaxTimeLimitMinutes);
        var session = Session.StartMultiUnitExam(multi.StudentId, multi.Subject.Id, multi.Units, plan, 20, questions, multi.Lessons, false, MultiUnitExamBuilder.Now);
        session.SaveExamAnswer(session.Items[1], SessionBuilder.AnswerB, Grace, MultiUnitExamBuilder.Now.AddMinutes(1));
        session.SubmitExam(new Dictionary<Guid, QuestionGrade> { [session.Items[1].QuestionId] = SessionBuilder.Grade(1m) }, MultiUnitExamBuilder.Now.AddMinutes(2));
        _currentUserService.UserId.Returns(multi.StudentId);
        SessionRepositoryStub.StubFind(_sessionRepository, session);
        _questionRepository.GetRevisionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(questions.SelectMany(x => x.Revisions).ToList());
        _questionRepository.FindAsync(Arg.Any<Expression<Func<Question, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Question>, IQueryable<Question>>?>(), Arg.Any<Func<IQueryable<Question>, IOrderedQueryable<Question>>?>(), Arg.Any<bool>())
            .Returns(call => questions.Where(call.Arg<Expression<Func<Question, bool>>>().Compile()).ToList());
        _lessonRepository.FindAsync(Arg.Any<Expression<Func<Lesson, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<Func<IQueryable<Lesson>, IOrderedQueryable<Lesson>>?>(), Arg.Any<bool>())
            .Returns(call => multi.Lessons.Where(call.Arg<Expression<Func<Lesson, bool>>>().Compile()).ToList());
        _unitRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<bool>()).Returns(call => multi.Units.FirstOrDefault(x => x.Id == call.ArgAt<Guid>(0)));
        _subjectRepository.GetByIdAsync(multi.Subject.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<bool>()).Returns(multi.Subject);

        var result = await _handler.Handle(new GetExamSessionQuery(session.Id), TestContext.Current.CancellationToken);

        result.Units.Select(x => (x.UnitId, x.Name)).Should().Equal((multi.Units[0].Id, "Mechanics"), (multi.Units[1].Id, "Waves"));
        (result.SubjectId, result.SubjectName).Should().Be((multi.Subject.Id, "Physics"));
        result.UnitBreakdown.Select(x => (x.UnitId, x.Name, x.QuestionCount, x.ScorePercent)).Should().Equal((multi.Units[0].Id, "Mechanics", 1, 100.00m), (multi.Units[1].Id, "Waves", 1, 0m));
    }

    private async Task AssertThrowsAsync<TException>(Guid sessionId, string errorCode) where TException : BaseException
    {
        var act = () => _handler.Handle(new GetExamSessionQuery(sessionId), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<TException>()).Which.ErrorCode.Should().Be(errorCode);
    }
}
