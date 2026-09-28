using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.QuestionValidation.RecordQuestionOpening;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.ReviewSessions;
using Elmanhg.Domain.Teachers;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.QuestionValidation.RecordQuestionOpening;

public sealed class RecordQuestionOpeningHandlerTests
{
    private readonly IReviewSessionRepository _reviewSessionRepository = Substitute.For<IReviewSessionRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly ITeacherSubjectRepository _teacherSubjectRepository = Substitute.For<ITeacherSubjectRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly QuestionBuilder _builder = new();
    private readonly RecordQuestionOpeningHandler _handler;

    public RecordQuestionOpeningHandlerTests()
    {
        _currentUserService.UserId.Returns(_builder.Teacher.Id);
        _teacherSubjectRepository.IsAssignedAsync(_builder.Teacher.Id, _builder.Subject.Id, Arg.Any<CancellationToken>()).Returns(true);
        _handler = new RecordQuestionOpeningHandler(_reviewSessionRepository, _questionRepository, _teacherSubjectRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_AssignedQuestion_RecordsOpeningAndSaves()
    {
        var session = StubSession(ReviewSession.Start(_builder.Teacher.Id, TimeSpan.FromHours(1)));
        var question = StubQuestion(_builder.Build());

        await _handler.Handle(new RecordQuestionOpeningCommand(session.Id, question.Id), TestContext.Current.CancellationToken);

        session.Openings.Should().ContainSingle().Which.QuestionId.Should().Be(question.Id);
        await _reviewSessionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new RecordQuestionOpeningCommand(Guid.NewGuid(), Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _reviewSessionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SessionMissing_ThrowsReviewSessionNotFound()
    {
        var question = StubQuestion(_builder.Build());

        var act = () => _handler.Handle(new RecordQuestionOpeningCommand(Guid.NewGuid(), question.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.ReviewSessionNotFound);
        await _reviewSessionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OtherTeachersSession_ThrowsReviewSessionNotFound()
    {
        var session = StubSession(ReviewSession.Start(Guid.NewGuid(), TimeSpan.FromHours(1)));
        var question = StubQuestion(_builder.Build());

        var act = () => _handler.Handle(new RecordQuestionOpeningCommand(session.Id, question.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.ReviewSessionNotFound);
        session.Openings.Should().BeEmpty();
        await _reviewSessionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_QuestionMissing_ThrowsQuestionNotFound()
    {
        var session = StubSession(ReviewSession.Start(_builder.Teacher.Id, TimeSpan.FromHours(1)));

        var act = () => _handler.Handle(new RecordQuestionOpeningCommand(session.Id, Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.QuestionNotFound);
        await _reviewSessionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OutOfScope_ThrowsSubjectOutOfScope()
    {
        var session = StubSession(ReviewSession.Start(_builder.Teacher.Id, TimeSpan.FromHours(1)));
        var question = StubQuestion(_builder.Build());
        _teacherSubjectRepository.IsAssignedAsync(_builder.Teacher.Id, _builder.Subject.Id, Arg.Any<CancellationToken>()).Returns(false);

        var act = () => _handler.Handle(new RecordQuestionOpeningCommand(session.Id, question.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ForbiddenCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.SubjectOutOfScope);
        session.Openings.Should().BeEmpty();
        await _reviewSessionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExpiredSession_ThrowsReviewSessionExpired()
    {
        var session = StubSession(ReviewSession.Start(_builder.Teacher.Id, TimeSpan.Zero));
        var question = StubQuestion(_builder.Build());

        var act = () => _handler.Handle(new RecordQuestionOpeningCommand(session.Id, question.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BusinessRuleViolationCoreException>()).Which.ErrorCode.Should().Be(DomainErrorCodes.ReviewSessionExpired);
        await _reviewSessionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private ReviewSession StubSession(ReviewSession session)
    {
        _reviewSessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<ReviewSession>, IQueryable<ReviewSession>>?>(), Arg.Any<bool>()).Returns(session);
        return session;
    }

    private Question StubQuestion(Question question)
    {
        _questionRepository.GetByIdAsync(question.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Question>, IQueryable<Question>>?>(), Arg.Any<bool>()).Returns(question);
        return question;
    }
}
