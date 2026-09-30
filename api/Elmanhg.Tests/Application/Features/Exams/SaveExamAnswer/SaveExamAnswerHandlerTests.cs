using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exams.SaveExamAnswer;
using Elmanhg.Application.Exams.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Questions.Schemas;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Sessions;
using Elmanhg.Tests.Application.Features.Sessions;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.Exams.SaveExamAnswer;

public sealed class SaveExamAnswerHandlerTests
{
    private static readonly DateTimeOffset Clock = ExamSessionBuilder.Now.AddMinutes(5);
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly ExamSessionBuilder _builder = new();
    private readonly List<Question> _questions;
    private readonly Session _session;
    private readonly SaveExamAnswerHandler _handler;

    public SaveExamAnswerHandlerTests()
    {
        _questions = _builder.BuildQuestions(2);
        _session = Session.StartUnitExam(_builder.StudentId, _builder.Questions.Unit, _builder.Blueprint(2), _questions, [_builder.Questions.Lesson], false, ExamSessionBuilder.Now);
        _currentUserService.UserId.Returns(_builder.StudentId);
        _timeProvider.GetUtcNow().Returns(Clock);
        SessionRepositoryStub.StubFind(_sessionRepository, _session);
        _questionRepository.GetRevisionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(_questions[0].Revisions);
        _handler = new SaveExamAnswerHandler(_sessionRepository, _questionRepository, Options.Create(new ExamsOptions()), Options.Create(new ContentOptions { QuestionEssayAnswerMaxLength = 20000 }), Options.Create(new SessionsOptions()), _timeProvider, _currentUserService);
    }

    [Fact]
    public async Task Handle_NoUser_ThrowsUnauthorized()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        await AssertThrowsAsync<UnauthorizedCoreException>(Command(SessionBuilder.AnswerB), ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_UnknownSession_ThrowsNotFound()
    {
        await AssertThrowsAsync<NotFoundCoreException>(Command(SessionBuilder.AnswerB) with { SessionId = Guid.NewGuid() }, ErrorCodes.SessionNotFound);
    }

    [Fact]
    public async Task Handle_QuestionNotInExam_ThrowsNotFound()
    {
        await AssertThrowsAsync<NotFoundCoreException>(Command(SessionBuilder.AnswerB) with { QuestionId = Guid.NewGuid() }, ErrorCodes.SessionQuestionNotFound);
    }

    [Fact]
    public async Task Handle_RevisionMissing_ThrowsNotFound()
    {
        _questionRepository.GetRevisionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(new List<QuestionRevision>());

        await AssertThrowsAsync<NotFoundCoreException>(Command(SessionBuilder.AnswerB), ErrorCodes.QuestionNotFound);
    }

    [Fact]
    public async Task Handle_WrongShape_ThrowsValidation()
    {
        await AssertThrowsAsync<ApplicationValidationCoreException>(Command("""{"optionId":5}"""), ErrorCodes.QuestionAnswerInvalid);
        _session.Items[0].SavedAnswer.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ValidAnswer_SavesCanonicalAnswer()
    {
        var result = await _handler.Handle(Command("""{ "optionId" : "b", "extra" : 1 }"""), TestContext.Current.CancellationToken);

        _session.Items[0].SavedAnswer.Should().Be(SessionBuilder.AnswerB);
        result.Should().Be(new ExamAnswerSavedResult(_questions[0].Id, Clock));
        _session.Attempts.Should().BeEmpty();
        await _sessionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PastDeadline_ThrowsExamTimeExpired()
    {
        _timeProvider.GetUtcNow().Returns(ExamSessionBuilder.Now.AddMinutes(31));

        await AssertThrowsAsync<BusinessRuleViolationCoreException>(Command(SessionBuilder.AnswerB), DomainErrorCodes.ExamTimeExpired);
        _session.Items[0].SavedAnswer.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Submitted_ThrowsAlreadySubmitted()
    {
        _session.SubmitExam(new Dictionary<Guid, QuestionGrade>(), ExamSessionBuilder.Now.AddMinutes(1));

        await AssertThrowsAsync<BusinessRuleViolationCoreException>(Command(SessionBuilder.AnswerB), DomainErrorCodes.SessionAlreadySubmitted);
    }

    [Fact]
    public async Task Handle_EssayOverMaxLength_ThrowsQuestionEssayAnswerTooLong()
    {
        var exam = new ExamSessionBuilder();
        var session = exam.BuildWithEssay();
        var essayItem = session.Items.Single(x => x.MaxScore == 5);
        _currentUserService.UserId.Returns(exam.StudentId);
        SessionRepositoryStub.StubFind(_sessionRepository, session);
        var essay = exam.Questions.Essay().Approved().Build();
        _questionRepository.GetRevisionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(essay.Revisions);
        var command = new SaveExamAnswerCommand(session.Id, essayItem.QuestionId, QuestionBuilder.Json($$"""{"text":"{{new string('ب', 20001)}}"}"""));

        await AssertThrowsAsync<ApplicationValidationCoreException>(command, ErrorCodes.QuestionEssayAnswerTooLong);
        essayItem.SavedAnswer.Should().BeNull();
    }

    [Fact]
    public async Task Handle_NonEssayOverAnswerCap_ThrowsAttemptAnswerTooLong()
    {
        await AssertThrowsAsync<ApplicationValidationCoreException>(Command($$"""{"optionId":"b","padding":"{{new string('x', 4000)}}"}"""), ErrorCodes.AttemptAnswerTooLong);
        _session.Items[0].SavedAnswer.Should().BeNull();
    }

    [Fact]
    public async Task Handle_MathStepsAnswerOverLimits_ThrowsAttemptAnswerTooLong()
    {
        var (session, math) = StartMathExam();
        var finalAnswer = new string('x', 201);

        await AssertThrowsAsync<ApplicationValidationCoreException>(new SaveExamAnswerCommand(session.Id, math.Id, QuestionBuilder.Json($$"""{"finalAnswer":"{{finalAnswer}}"}""")), ErrorCodes.AttemptAnswerTooLong);
        session.GetItem(math.Id)!.SavedAnswer.Should().BeNull();
    }

    [Fact]
    public async Task Handle_MathStepsAnswer_SavesCanonicalAnswer()
    {
        var (session, math) = StartMathExam();

        await _handler.Handle(new SaveExamAnswerCommand(session.Id, math.Id, QuestionBuilder.Json("""{"steps":[" 2x = 4 ",""],"finalAnswer":" x=2 "}""")), TestContext.Current.CancellationToken);

        QuestionJson.AreEquivalent(session.GetItem(math.Id)!.SavedAnswer!, """{"steps":["2x = 4"],"finalAnswer":"x=2"}""").Should().BeTrue();
        await _sessionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private (Session Session, Question Math) StartMathExam()
    {
        var math = _builder.Questions.MathSteps().Approved().Build();
        var session = Session.StartUnitExam(_builder.StudentId, _builder.Questions.Unit, _builder.Blueprint(1), [math], [_builder.Questions.Lesson], false, ExamSessionBuilder.Now);
        SessionRepositoryStub.StubFind(_sessionRepository, session);
        _questionRepository.GetRevisionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(math.Revisions);
        return (session, math);
    }

    private SaveExamAnswerCommand Command(string answer) => new(_session.Id, _questions[0].Id, QuestionBuilder.Json(answer));

    private async Task AssertThrowsAsync<TException>(SaveExamAnswerCommand command, string errorCode) where TException : BaseException
    {
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<TException>()).Which.ErrorCode.Should().Be(errorCode);
        await _sessionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
