using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Localization;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Sessions.SubmitAnswer;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.Storage;
using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Questions.Schemas;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Tests.Application.Features.Mastery;
using Elmanhg.Tests.Application.Features.Subscriptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;
using System.Text.Json;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.Sessions.SubmitAnswer;

public sealed class SubmitAnswerHandlerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly IAiMathCheckClient _mathCheckClient = Substitute.For<IAiMathCheckClient>();
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly IQuestionMasteryRepository _questionMasteryRepository = Substitute.For<IQuestionMasteryRepository>();
    private readonly IEssayGradeRepository _essayGradeRepository = Substitute.For<IEssayGradeRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ISubscriptionRepository _subscriptionRepository = Substitute.For<ISubscriptionRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly SessionBuilder _builder = new();
    private readonly List<Question> _questions;
    private readonly Session _session;
    private readonly SubmitAnswerHandler _handler;

    public SubmitAnswerHandlerTests()
    {
        _currentUserService.UserId.Returns(_builder.StudentId);
        _questions = _builder.BuildQuestions(2);
        _session = Session.StartQuiz(_builder.StudentId, _builder.Questions.Lesson, _questions, isTestMode: false);
        SessionRepositoryStub.StubFind(_sessionRepository, _session);
        _questionRepository.GetRevisionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(_questions[0].Revisions);
        _timeProvider.GetUtcNow().Returns(T0);
        SubscriptionRepositoryStub.Stub(_subscriptionRepository, SubscriptionRepositoryStub.EntitledBase(_builder.StudentId, T0));
        _handler = new SubmitAnswerHandler(_sessionRepository, _questionRepository, _questionMasteryRepository, _lessonRepository, _subscriptionRepository, _essayGradeRepository, Options.Create(new MasteryOptions()), Options.Create(new SubscriptionsOptions()), Options.Create(new ContentOptions { QuestionEssayAnswerMaxLength = 20000 }), Options.Create(new SessionsOptions()), _timeProvider, _currentUserService, Substitute.For<ILocalizer>(), _mathCheckClient, Substitute.For<IFileStorage>());
    }

    private Guid QuestionId => _questions[0].Id;

    [Fact]
    public async Task Handle_FirstAnswer_RecordsGradedAttemptAndSaves()
    {
        var result = await _handler.Handle(Command("""{ "optionId" : "b", "extra" : 1 }"""), TestContext.Current.CancellationToken);

        result.Attempt!.Outcome.Should().Be("Correct");
        result.Attempt.Score.Should().Be(1m);
        result.CorrectAnswer.Should().NotBeNull();
        result.Explanation.Should().Be(QuestionBuilder.McqContent().Explanation);
        _session.Attempts.Should().ContainSingle().Which.Answer.Should().Be(SessionBuilder.AnswerB);
        await _sessionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_QuestionEditedAfterServing_GradesServedVersion()
    {
        _questions[0].Update(QuestionType.Mcq, QuestionBuilder.McqContent() with { GradingSpec = """{"correctOptionId":"a"}""" }, new QuestionMetadata(QuestionDifficulty.Medium, null, []), _builder.Questions.Lesson, Guid.NewGuid());

        var result = await _handler.Handle(Command(SessionBuilder.AnswerB), TestContext.Current.CancellationToken);

        _questions[0].Revisions.Should().HaveCount(2);
        result.Attempt!.Outcome.Should().Be("Correct");
        (result.QuestionVersion, _session.Attempts.Single().QuestionVersion).Should().Be((1, 1));
    }

    [Fact]
    public async Task Handle_SameAnswerResubmitted_ReturnsOriginalAttempt()
    {
        var first = await _handler.Handle(Command(SessionBuilder.AnswerB), TestContext.Current.CancellationToken);

        var second = await _handler.Handle(Command(SessionBuilder.AnswerB), TestContext.Current.CancellationToken);

        second.Attempt!.Id.Should().Be(first.Attempt!.Id);
        _session.Attempts.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_FirstAnswer_StartsQuestionMastery()
    {
        var result = await _handler.Handle(Command(SessionBuilder.AnswerB), TestContext.Current.CancellationToken);

        await _questionMasteryRepository.Received(1).AddAsync(Arg.Is<QuestionMastery>(m => m.StudentId == _builder.StudentId && m.QuestionId == QuestionId && m.LatestAttemptId == result.Attempt!.Id && !m.IsMastered), Arg.Any<CancellationToken>());
        await _sessionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SecondCorrectAnswer_MarksExistingMasteryMastered()
    {
        var row = QuestionMastery.Start(_builder.StudentId, QuestionId, new MasteryAttempt(Guid.NewGuid(), 1m, DateTimeOffset.UtcNow.AddDays(-1)));
        QuestionMasteryRepositoryStub.StubFind(_questionMasteryRepository, row);

        var result = await _handler.Handle(Command(SessionBuilder.AnswerB), TestContext.Current.CancellationToken);

        row.IsMastered.Should().BeTrue();
        row.LatestAttemptId.Should().Be(result.Attempt!.Id);
        await _questionMasteryRepository.DidNotReceive().AddAsync(Arg.Any<QuestionMastery>(), Arg.Any<CancellationToken>());
        await _sessionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReplayedAnswer_DoesNotRecordMasteryAgain()
    {
        var first = await _handler.Handle(Command(SessionBuilder.AnswerB), TestContext.Current.CancellationToken);

        var second = await _handler.Handle(Command(SessionBuilder.AnswerB), TestContext.Current.CancellationToken);

        second.Attempt!.Id.Should().Be(first.Attempt!.Id);
        await _questionMasteryRepository.Received(1).AddAsync(Arg.Any<QuestionMastery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TestModeSession_DoesNotRecordMastery()
    {
        var testSession = Session.StartQuiz(_builder.StudentId, _builder.Questions.Lesson, _questions, isTestMode: true);
        SessionRepositoryStub.StubFind(_sessionRepository, testSession);

        await _handler.Handle(Command(SessionBuilder.AnswerB) with { SessionId = testSession.Id }, TestContext.Current.CancellationToken);

        testSession.Attempts.Should().ContainSingle();
        await _questionMasteryRepository.DidNotReceive().AddAsync(Arg.Any<QuestionMastery>(), Arg.Any<CancellationToken>());
        await _sessionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        await AssertThrowsAsync<UnauthorizedCoreException>(Command(SessionBuilder.AnswerB), ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_SessionMissing_ThrowsSessionNotFound()
    {
        await AssertThrowsAsync<NotFoundCoreException>(Command(SessionBuilder.AnswerB) with { SessionId = Guid.NewGuid() }, ErrorCodes.SessionNotFound);
    }

    [Fact]
    public async Task Handle_OtherStudentsSession_ThrowsSessionNotFound()
    {
        var other = Session.StartQuiz(Guid.NewGuid(), _builder.Questions.Lesson, _questions, isTestMode: false);
        SessionRepositoryStub.StubFind(_sessionRepository, other);

        await AssertThrowsAsync<NotFoundCoreException>(Command(SessionBuilder.AnswerB) with { SessionId = other.Id }, ErrorCodes.SessionNotFound);
        other.Attempts.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_QuestionNotInSession_ThrowsSessionQuestionNotFound()
    {
        await AssertThrowsAsync<NotFoundCoreException>(Command(SessionBuilder.AnswerB) with { QuestionId = Guid.NewGuid() }, ErrorCodes.SessionQuestionNotFound);
    }

    [Fact]
    public async Task Handle_RevisionMissing_ThrowsQuestionNotFound()
    {
        _questionRepository.GetRevisionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(new List<QuestionRevision>());

        await AssertThrowsAsync<NotFoundCoreException>(Command(SessionBuilder.AnswerB), ErrorCodes.QuestionNotFound);
    }

    [Fact]
    public async Task Handle_AnswerShapeWrongForType_ThrowsQuestionAnswerInvalid()
    {
        await AssertThrowsAsync<ApplicationValidationCoreException>(Command("""{"optionId":5}"""), ErrorCodes.QuestionAnswerInvalid);
        _session.Attempts.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_SubmittedSession_ThrowsSessionAlreadySubmitted()
    {
        _session.Submit();

        await AssertThrowsAsync<BusinessRuleViolationCoreException>(Command(SessionBuilder.AnswerB), DomainErrorCodes.SessionAlreadySubmitted);
    }

    [Fact]
    public async Task Handle_DifferentAnswerForAnsweredQuestion_ThrowsSessionQuestionAlreadyAnswered()
    {
        _session.RecordAttempt(_session.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);

        await AssertThrowsAsync<ConflictCoreException>(Command(SessionBuilder.AnswerA), DomainErrorCodes.SessionQuestionAlreadyAnswered);
        _session.Attempts.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_ExamSession_ThrowsNotFound()
    {
        var exam = Session.StartUnitExam(_builder.StudentId, _builder.Questions.Unit, ExamBlueprint.CreateForUnit(_builder.Questions.Unit, ExamBlueprintBuilder.Shape(new ExamTypeCount(QuestionType.Mcq, 2)), ExamBlueprintBuilder.Plenty(), Guid.NewGuid()), _questions, [_builder.Questions.Lesson], false, DateTimeOffset.UtcNow);
        SessionRepositoryStub.StubFind(_sessionRepository, exam);

        await AssertThrowsAsync<NotFoundCoreException>(Command(SessionBuilder.AnswerB) with { SessionId = exam.Id }, ErrorCodes.SessionNotFound);
        exam.Attempts.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WrittenEssay_SavesPendingAnswerAndRequestsGrade()
    {
        var (session, essay) = SeedEssaySession();

        var result = await _handler.Handle(EssayCommand(session, essay, "  القصور الذاتي  "), TestContext.Current.CancellationToken);

        await _essayGradeRepository.Received(1).AddAsync(Arg.Is<EssayGrade>(x => x.StudentId == _builder.StudentId && x.SessionId == session.Id && x.SubjectId == essay.SubjectId && x.QuestionId == essay.Id && x.QuestionVersion == 1 && x.MaxScore == 5 && x.ReadAnswerText() == "القصور الذاتي" && x.TimeTakenMilliseconds <= 1000 && x.Status == EssayGradeStatus.Pending), Arg.Any<CancellationToken>());
        result.Attempt.Should().BeNull();
        result.PendingAnswer!.Value.GetProperty("text").GetString().Should().Be("القصور الذاتي");
        session.Attempts.Should().BeEmpty();
        await _sessionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MathStepsAnswer_RecordsGradeFromMathCheck()
    {
        var (session, math) = StartMathQuiz();
        ReplyMath(MathAnswerVerdict.Equivalent);

        var result = await _handler.Handle(MathCommand(session, math, """{"steps":[" 2x = 4 ","  "],"finalAnswer":" x = 2 "}"""), TestContext.Current.CancellationToken);

        (result.Attempt!.Outcome, result.Attempt.Score, result.Attempt.AwaitsReview).Should().Be(("Correct", 2m, false));
        QuestionJson.AreEquivalent(session.Attempts.Single().Answer, """{"steps":["2x = 4"],"finalAnswer":"x = 2"}""").Should().BeTrue();
        await _mathCheckClient.Received(1).CheckAsync(Arg.Is<AiMathCheckRequest>(x => x.Answer == "x = 2"), Arg.Any<CancellationToken>());
        await _sessionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WrittenEssayReplay_RequestsNoSecondGrade()
    {
        var (session, essay) = SeedEssaySession();
        var first = await _handler.Handle(EssayCommand(session, essay, "القصور الذاتي"), TestContext.Current.CancellationToken);
        _essayGradeRepository.ClearReceivedCalls();

        var second = await _handler.Handle(EssayCommand(session, essay, "القصور الذاتي"), TestContext.Current.CancellationToken);

        await _essayGradeRepository.DidNotReceive().AddAsync(Arg.Any<EssayGrade>(), Arg.Any<CancellationToken>());
        second.PendingAnswer!.Value.GetRawText().Should().Be(first.PendingAnswer!.Value.GetRawText());
    }

    [Fact]
    public async Task Handle_BlankEssay_RecordsUnansweredAttempt()
    {
        var (session, essay) = SeedEssaySession();

        var result = await _handler.Handle(EssayCommand(session, essay, "   "), TestContext.Current.CancellationToken);

        var attempt = session.Attempts.Should().ContainSingle().Subject;
        (attempt.Score, attempt.GradedBy).Should().Be((0m, AttemptGrader.Auto));
        result.Attempt!.Outcome.Should().Be("Incorrect");
        await _essayGradeRepository.DidNotReceive().AddAsync(Arg.Any<EssayGrade>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EssayOverMaxLength_ThrowsQuestionEssayAnswerTooLong()
    {
        var (session, essay) = SeedEssaySession();

        await AssertThrowsAsync<ApplicationValidationCoreException>(EssayCommand(session, essay, new string('ب', 20001)), ErrorCodes.QuestionEssayAnswerTooLong);
        await _essayGradeRepository.DidNotReceive().AddAsync(Arg.Any<EssayGrade>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NonEssayOverAnswerCap_ThrowsAttemptAnswerTooLong()
    {
        var command = Command(SessionBuilder.AnswerB) with { Answer = QuestionBuilder.Json($$"""{"optionId":"b","padding":"{{new string('x', 4000)}}"}""") };

        await AssertThrowsAsync<ApplicationValidationCoreException>(command, ErrorCodes.AttemptAnswerTooLong);
        _session.Attempts.Should().BeEmpty();
    }

    private (Session Session, Question Essay) SeedEssaySession()
    {
        var mcq = _builder.Questions.Approved().Build();
        var essay = _builder.Questions.Essay().Approved().Build();
        var session = Session.StartQuiz(_builder.StudentId, _builder.Questions.Lesson, [mcq, essay], isTestMode: false);
        SessionRepositoryStub.StubFind(_sessionRepository, session);
        _questionRepository.GetRevisionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(essay.Revisions);
        _questionRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<Question, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Question>, IQueryable<Question>>?>(), Arg.Any<Func<IQueryable<Question>, IOrderedQueryable<Question>>?>(), Arg.Any<bool>())
            .Returns(call => new[] { essay }.FirstOrDefault(call.Arg<Expression<Func<Question, bool>>>().Compile()));
        return (session, essay);
    }

    private static SubmitAnswerCommand EssayCommand(Session session, Question essay, string text) => new(session.Id, essay.Id, QuestionBuilder.Json(JsonSerializer.Serialize(new { text })), 1000);

    [Fact]
    public async Task Handle_MathStepsAnswerOverLimits_ThrowsAttemptAnswerTooLong()
    {
        var (session, math) = StartMathQuiz();
        var steps = string.Join(",", Enumerable.Repeat("\"x\"", 21));

        await AssertThrowsAsync<ApplicationValidationCoreException>(MathCommand(session, math, $$"""{"steps":[{{steps}}],"finalAnswer":"x = 2"}"""), ErrorCodes.AttemptAnswerTooLong);
        await _mathCheckClient.DidNotReceive().CheckAsync(Arg.Any<AiMathCheckRequest>(), Arg.Any<CancellationToken>());
        session.Attempts.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MathCheckUnchecked_RecordsAttemptForReviewWithoutMastery()
    {
        var (session, math) = StartMathQuiz();
        ReplyMath(MathAnswerVerdict.Unchecked);

        var result = await _handler.Handle(MathCommand(session, math, """{"steps":["2x = 4"],"finalAnswer":"x = 2"}"""), TestContext.Current.CancellationToken);

        (result.Attempt!.Score, result.Attempt.AwaitsReview).Should().Be((0m, true));
        session.Attempts.Single().ReadFeedback().Should().Be(GradeFeedback.MathUnchecked);
        await _questionMasteryRepository.DidNotReceive().AddAsync(Arg.Any<QuestionMastery>(), Arg.Any<CancellationToken>());
        await _sessionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private (Session Session, Question Math) StartMathQuiz()
    {
        var math = _builder.Questions.MathSteps().Approved().Build();
        var session = Session.StartQuiz(_builder.StudentId, _builder.Questions.Lesson, [math], isTestMode: false);
        SessionRepositoryStub.StubFind(_sessionRepository, session);
        _questionRepository.GetRevisionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(math.Revisions);
        return (session, math);
    }

    private void ReplyMath(MathAnswerVerdict verdict) => _mathCheckClient.CheckAsync(Arg.Any<AiMathCheckRequest>(), Arg.Any<CancellationToken>()).Returns(new AiMathCheckResult(verdict, null, []));

    private static SubmitAnswerCommand MathCommand(Session session, Question math, string answer) => new(session.Id, math.Id, QuestionBuilder.Json(answer), 1000);

    [Fact]
    public async Task Handle_DragDropAnswer_RecordsPartialAttempt()
    {
        var command = StartDragDropQuiz("""{"placements":[{"zoneId":"z1","itemIds":["i1","i2"]}]}""");

        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        (result.Attempt!.Outcome, result.Attempt.Score).Should().Be(("Partial", 2m));
        await _sessionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DragDropNullItemId_ThrowsQuestionAnswerInvalid()
    {
        var command = StartDragDropQuiz("""{"placements":[{"zoneId":"z1","itemIds":[null]}]}""");

        await AssertThrowsAsync<ApplicationValidationCoreException>(command, ErrorCodes.QuestionAnswerInvalid);
    }

    private SubmitAnswerCommand StartDragDropQuiz(string answer)
    {
        var session = _builder.BuildWithDragDrop();
        SessionRepositoryStub.StubFind(_sessionRepository, session);
        _questionRepository.GetRevisionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(new QuestionBuilder().DragDrop().Approved().Build().Revisions);
        return new SubmitAnswerCommand(session.Id, session.Items[1].QuestionId, QuestionBuilder.Json(answer), 1000);
    }

    private SubmitAnswerCommand Command(string answer) => new(_session.Id, QuestionId, QuestionBuilder.Json(answer), 1000);

    private async Task AssertThrowsAsync<TException>(SubmitAnswerCommand command, string errorCode) where TException : BaseException
    {
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<TException>()).Which.ErrorCode.Should().Be(errorCode);
        await _sessionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
