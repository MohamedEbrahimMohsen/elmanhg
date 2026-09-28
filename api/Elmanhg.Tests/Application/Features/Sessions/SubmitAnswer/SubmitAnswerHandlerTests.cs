using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Localization;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Sessions.SubmitAnswer;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Tests.Application.Features.Mastery;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.Sessions.SubmitAnswer;

public sealed class SubmitAnswerHandlerTests
{
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly IQuestionMasteryRepository _questionMasteryRepository = Substitute.For<IQuestionMasteryRepository>();
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
        _handler = new SubmitAnswerHandler(_sessionRepository, _questionRepository, _questionMasteryRepository, Options.Create(new MasteryOptions()), _currentUserService, Substitute.For<ILocalizer>());
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

    private SubmitAnswerCommand Command(string answer) => new(_session.Id, QuestionId, QuestionBuilder.Json(answer), 1000);

    private async Task AssertThrowsAsync<TException>(SubmitAnswerCommand command, string errorCode) where TException : BaseException
    {
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<TException>()).Which.ErrorCode.Should().Be(errorCode);
        await _sessionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
