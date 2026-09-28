using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.QuestionValidation.BulkApproveQuestions;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.ReviewSessions;
using Elmanhg.Domain.Teachers;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.QuestionValidation.BulkApproveQuestions;

public sealed class BulkApproveQuestionsHandlerTests
{
    private readonly IReviewSessionRepository _reviewSessionRepository = Substitute.For<IReviewSessionRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly ITeacherSubjectRepository _teacherSubjectRepository = Substitute.For<ITeacherSubjectRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly QuestionBuilder _builder = new();
    private readonly Question _first;
    private readonly Question _second;
    private readonly BulkApproveQuestionsHandler _handler;

    public BulkApproveQuestionsHandlerTests()
    {
        _first = _builder.Build();
        _second = _builder.Build();
        _currentUserService.UserId.Returns(_builder.Teacher.Id);
        ReturnsAssignments([TeacherSubject.Create(_builder.Teacher, _builder.Subject, Guid.NewGuid())]);
        ReturnsQuestions([_first, _second]);
        _handler = new BulkApproveQuestionsHandler(_reviewSessionRepository, _questionRepository, _teacherSubjectRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_AllOpened_ApprovesEveryQuestionAndSaves()
    {
        var session = OpenedSession(_first, _second);

        var result = await _handler.Handle(Command(session), TestContext.Current.CancellationToken);

        result.ApprovedCount.Should().Be(2);
        _first.ValidationStatus.Should().Be(QuestionValidationStatus.Approved);
        _second.ValidationStatus.Should().Be(QuestionValidationStatus.Approved);
        await _questionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        var session = OpenedSession(_first, _second);
        _currentUserService.UserId.Returns((Guid?)null);

        await ShouldThrow<UnauthorizedCoreException>(Command(session), ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_SessionMissing_ThrowsReviewSessionNotFound()
    {
        await ShouldThrow<NotFoundCoreException>(new BulkApproveQuestionsCommand(Guid.NewGuid(), [_first.Id, _second.Id]), ErrorCodes.ReviewSessionNotFound);
    }

    [Fact]
    public async Task Handle_OtherTeachersSession_ThrowsReviewSessionNotFound()
    {
        var session = Stub(ReviewSession.Start(Guid.NewGuid(), TimeSpan.FromHours(1)));

        await ShouldThrow<NotFoundCoreException>(Command(session), ErrorCodes.ReviewSessionNotFound);
    }

    [Fact]
    public async Task Handle_QuestionMissing_ThrowsQuestionNotFound()
    {
        var session = OpenedSession(_first, _second);
        ReturnsQuestions([_first]);

        await ShouldThrow<NotFoundCoreException>(Command(session), ErrorCodes.QuestionNotFound);
    }

    [Fact]
    public async Task Handle_QuestionOutOfScope_ThrowsSubjectOutOfScope()
    {
        var session = OpenedSession(_first, _second);
        ReturnsAssignments([]);

        await ShouldThrow<ForbiddenCoreException>(Command(session), ErrorCodes.SubjectOutOfScope);
    }

    [Fact]
    public async Task Handle_OneNotOpened_ThrowsQuestionNotOpenedInSessionAndSavesNothing()
    {
        var session = OpenedSession(_first);

        await ShouldThrow<BusinessRuleViolationCoreException>(Command(session), DomainErrorCodes.QuestionNotOpenedInSession);
        _second.ValidationStatus.Should().Be(QuestionValidationStatus.Pending);
    }

    [Fact]
    public async Task Handle_OpenedEarlierVersion_ThrowsQuestionNotOpenedInSession()
    {
        var session = OpenedSession(_first, _second);
        _second.Update(QuestionType.Mcq, QuestionBuilder.McqContent() with { Stem = "<p>3 + 3 = ?</p>" }, new QuestionMetadata(QuestionDifficulty.Medium, null, []), _builder.Lesson, Guid.NewGuid());

        await ShouldThrow<BusinessRuleViolationCoreException>(Command(session), DomainErrorCodes.QuestionNotOpenedInSession);
    }

    [Fact]
    public async Task Handle_ExpiredSession_ThrowsReviewSessionExpired()
    {
        var session = Stub(ReviewSession.Start(_builder.Teacher.Id, TimeSpan.Zero));

        await ShouldThrow<BusinessRuleViolationCoreException>(Command(session), DomainErrorCodes.ReviewSessionExpired);
    }

    private async Task ShouldThrow<TException>(BulkApproveQuestionsCommand command, string errorCode) where TException : BaseException
    {
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<TException>()).Which.ErrorCode.Should().Be(errorCode);
        await _questionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private BulkApproveQuestionsCommand Command(ReviewSession session) => new(session.Id, [_first.Id, _second.Id]);

    private ReviewSession OpenedSession(params Question[] questions)
    {
        var session = ReviewSession.Start(_builder.Teacher.Id, TimeSpan.FromHours(1));
        foreach (var question in questions)
        {
            session.RecordOpening(question);
        }

        return Stub(session);
    }

    private ReviewSession Stub(ReviewSession session)
    {
        _reviewSessionRepository.GetByIdAsync(session.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<ReviewSession>, IQueryable<ReviewSession>>?>(), Arg.Any<bool>()).Returns(session);
        return session;
    }

    private void ReturnsQuestions(List<Question> questions)
    {
        _questionRepository.FindAsync(Arg.Any<Expression<Func<Question, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Question>, IQueryable<Question>>?>(), Arg.Any<Func<IQueryable<Question>, IOrderedQueryable<Question>>?>(), Arg.Any<bool>()).Returns(questions);
    }

    private void ReturnsAssignments(List<TeacherSubject> assignments)
    {
        _teacherSubjectRepository.FindAsync(Arg.Any<Expression<Func<TeacherSubject, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<TeacherSubject>, IQueryable<TeacherSubject>>?>(), Arg.Any<Func<IQueryable<TeacherSubject>, IOrderedQueryable<TeacherSubject>>?>(), Arg.Any<bool>()).Returns(assignments);
    }
}
