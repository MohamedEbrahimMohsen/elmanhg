using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Localization;
using Elmanhg.Application.Exams.SubmitExam;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using Elmanhg.Tests.Application.Features.Sessions;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Exams.SubmitExam;

public sealed class SubmitExamHandlerTests
{
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(30);
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly IQuestionMasteryRepository _questionMasteryRepository = Substitute.For<IQuestionMasteryRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly ExamSessionBuilder _builder = new();
    private readonly List<Question> _questions;
    private readonly List<QuestionMastery> _masteries = [];
    private readonly SubmitExamHandler _handler;
    private Session _session;

    public SubmitExamHandlerTests()
    {
        _questions = _builder.BuildQuestions(2);
        _session = StartExam(isTestMode: false);
        _currentUserService.UserId.Returns(_builder.StudentId);
        _timeProvider.GetUtcNow().Returns(ExamSessionBuilder.Now.AddMinutes(10));
        _questionRepository.GetRevisionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(_questions.SelectMany(x => x.Revisions).ToList());
        _questionRepository.FindAsync(Arg.Any<Expression<Func<Question, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Question>, IQueryable<Question>>?>(), Arg.Any<Func<IQueryable<Question>, IOrderedQueryable<Question>>?>(), Arg.Any<bool>())
            .Returns(call => _questions.Where(call.Arg<Expression<Func<Question, bool>>>().Compile()).ToList());
        _lessonRepository.FindAsync(Arg.Any<Expression<Func<Lesson, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<Func<IQueryable<Lesson>, IOrderedQueryable<Lesson>>?>(), Arg.Any<bool>())
            .Returns([_builder.Questions.Lesson]);
        _questionMasteryRepository.FindAsync(Arg.Any<Expression<Func<QuestionMastery, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<QuestionMastery>, IQueryable<QuestionMastery>>?>(), Arg.Any<Func<IQueryable<QuestionMastery>, IOrderedQueryable<QuestionMastery>>?>(), Arg.Any<bool>())
            .Returns(call => _masteries.Where(call.Arg<Expression<Func<QuestionMastery, bool>>>().Compile()).ToList());
        _unitRepository.GetByIdAsync(_builder.Questions.Unit.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<bool>()).Returns(_builder.Questions.Unit);
        _subjectRepository.GetByIdAsync(_builder.Questions.Subject.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<bool>()).Returns(_builder.Questions.Subject);
        _handler = new SubmitExamHandler(_sessionRepository, _questionRepository, _questionMasteryRepository, _lessonRepository, _unitRepository, _subjectRepository, Options.Create(new ExamsOptions()), Options.Create(new MasteryOptions()), _timeProvider, _currentUserService, Substitute.For<ILocalizer>());
    }

    private Guid QuestionId => _questions[0].Id;

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
        _session.IsSubmitted.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_SavedAnswers_GradesAndScores()
    {
        var result = await _handler.Handle(new SubmitExamCommand(_session.Id), TestContext.Current.CancellationToken);

        result.ScorePercent.Should().Be(50.00m);
        result.IsPassed.Should().BeTrue();
        result.Items[0].Attempt!.Outcome.Should().Be("Correct");
        result.Items[1].Attempt.Should().BeNull();
        _session.Attempts.Should().ContainSingle().Which.QuestionId.Should().Be(QuestionId);
        await _sessionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NewQuestion_StartsMastery()
    {
        await _handler.Handle(new SubmitExamCommand(_session.Id), TestContext.Current.CancellationToken);

        var attemptId = _session.Attempts.Single().Id;
        await _questionMasteryRepository.Received(1).AddRangeAsync(Arg.Is<List<QuestionMastery>>(list => list.Count == 1 && list[0].QuestionId == QuestionId && list[0].StudentId == _builder.StudentId && list[0].LatestAttemptId == attemptId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExistingMastery_RecordsAttempt()
    {
        var row = QuestionMastery.Start(_builder.StudentId, QuestionId, new MasteryAttempt(Guid.NewGuid(), 1m, ExamSessionBuilder.Now.AddDays(-1)));
        _masteries.Add(row);

        await _handler.Handle(new SubmitExamCommand(_session.Id), TestContext.Current.CancellationToken);

        row.LatestAttemptId.Should().Be(_session.Attempts.Single().Id);
        row.IsMastered.Should().BeTrue();
        await _questionMasteryRepository.DidNotReceive().AddRangeAsync(Arg.Any<List<QuestionMastery>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TestMode_DoesNotTouchMastery()
    {
        _session = StartExam(isTestMode: true);

        var result = await _handler.Handle(new SubmitExamCommand(_session.Id), TestContext.Current.CancellationToken);

        result.SubmittedAt.Should().NotBeNull();
        await _questionMasteryRepository.DidNotReceive().FindAsync(Arg.Any<Expression<Func<QuestionMastery, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<QuestionMastery>, IQueryable<QuestionMastery>>?>(), Arg.Any<Func<IQueryable<QuestionMastery>, IOrderedQueryable<QuestionMastery>>?>(), Arg.Any<bool>());
        await _questionMasteryRepository.DidNotReceive().AddRangeAsync(Arg.Any<List<QuestionMastery>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AlreadySubmitted_ReturnsSameResultWithoutNewAttempts()
    {
        var first = await _handler.Handle(new SubmitExamCommand(_session.Id), TestContext.Current.CancellationToken);

        var second = await _handler.Handle(new SubmitExamCommand(_session.Id), TestContext.Current.CancellationToken);

        (second.ScorePercent, second.SubmittedAt).Should().Be((first.ScorePercent, first.SubmittedAt));
        _session.Attempts.Should().HaveCount(1);
    }

    private Session StartExam(bool isTestMode)
    {
        var session = Session.StartUnitExam(_builder.StudentId, _builder.Questions.Unit, _builder.Blueprint(2), _questions, [_builder.Questions.Lesson], isTestMode, ExamSessionBuilder.Now);
        session.SaveExamAnswer(session.Items[0], SessionBuilder.AnswerB, Grace, ExamSessionBuilder.Now.AddMinutes(1));
        SessionRepositoryStub.StubFind(_sessionRepository, session);
        return session;
    }

    private async Task AssertThrowsAsync<TException>(Guid sessionId, string errorCode) where TException : BaseException
    {
        var act = () => _handler.Handle(new SubmitExamCommand(sessionId), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<TException>()).Which.ErrorCode.Should().Be(errorCode);
        await _sessionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
