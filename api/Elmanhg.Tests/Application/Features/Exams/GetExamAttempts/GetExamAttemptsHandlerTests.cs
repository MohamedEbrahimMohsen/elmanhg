using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exams.GetExamAttempts;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Tests.Application.Features.Sessions;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Exams.GetExamAttempts;

public sealed class GetExamAttemptsHandlerTests
{
    private static readonly DateTimeOffset Day = new(2026, 9, 28, 10, 0, 0, TimeSpan.Zero);
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly ExamSessionBuilder _builder = new();
    private readonly GetExamAttemptsHandler _handler;

    public GetExamAttemptsHandlerTests()
    {
        _currentUserService.UserId.Returns(_builder.StudentId);
        SessionRepositoryStub.StubFind(_sessionRepository);
        _handler = new GetExamAttemptsHandler(_sessionRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        await AssertThrowsAsync<UnauthorizedCoreException>(Guid.NewGuid(), ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_UnknownSession_ThrowsSessionNotFound()
    {
        await AssertThrowsAsync<NotFoundCoreException>(Guid.NewGuid(), ErrorCodes.SessionNotFound);
    }

    [Fact]
    public async Task Handle_QuizSession_ThrowsSessionNotFound()
    {
        var quiz = Session.StartQuiz(_builder.StudentId, _builder.Questions.Lesson, _builder.BuildQuestions(2), isTestMode: false);
        SessionRepositoryStub.StubFind(_sessionRepository, quiz);

        await AssertThrowsAsync<NotFoundCoreException>(quiz.Id, ErrorCodes.SessionNotFound);
    }

    [Fact]
    public async Task Handle_UnitExam_ReturnsAttemptsOfTheSessionScope()
    {
        var session = _builder.Build();
        SessionRepositoryStub.StubFind(_sessionRepository, session);
        List<ExamAttemptSummary> summaries = [new(Guid.NewGuid(), Day.AddDays(2), 65m), new(session.Id, Day, 90m)];
        _sessionRepository.GetExamAttemptsAsync(_builder.StudentId, SessionKind.UnitExam, session.ScopeKey, Arg.Any<CancellationToken>()).Returns(summaries);

        var result = await _handler.Handle(new GetExamAttemptsQuery(session.Id), TestContext.Current.CancellationToken);

        result.Attempts.Select(x => x.SessionId).Should().Equal(summaries[0].SessionId, session.Id);
        result.BestScorePercent.Should().Be(90m);
        result.Attempts.Select(x => x.IsBest).Should().Equal(false, true);
    }

    [Fact]
    public async Task Handle_MultiUnitExam_UsesMultiUnitKindAndScopeKey()
    {
        var multi = new MultiUnitExamBuilder();
        var session = multi.Build();
        _currentUserService.UserId.Returns(multi.StudentId);
        SessionRepositoryStub.StubFind(_sessionRepository, session);
        _sessionRepository.GetExamAttemptsAsync(multi.StudentId, SessionKind.MultiUnitExam, session.ScopeKey, Arg.Any<CancellationToken>()).Returns([new ExamAttemptSummary(session.Id, Day, 70m)]);

        var result = await _handler.Handle(new GetExamAttemptsQuery(session.Id), TestContext.Current.CancellationToken);

        var attempt = result.Attempts.Should().ContainSingle().Subject;
        (attempt.SessionId, attempt.IsBest).Should().Be((session.Id, true));
    }

    private async Task AssertThrowsAsync<TException>(Guid sessionId, string errorCode) where TException : BaseException
    {
        var act = () => _handler.Handle(new GetExamAttemptsQuery(sessionId), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<TException>()).Which.ErrorCode.Should().Be(errorCode);
        await _sessionRepository.DidNotReceive().GetExamAttemptsAsync(Arg.Any<Guid>(), Arg.Any<SessionKind>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
