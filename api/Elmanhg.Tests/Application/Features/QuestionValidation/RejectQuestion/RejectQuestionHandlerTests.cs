using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.QuestionValidation.RejectQuestion;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Teachers;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.QuestionValidation.RejectQuestion;

public sealed class RejectQuestionHandlerTests
{
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly ITeacherSubjectRepository _teacherSubjectRepository = Substitute.For<ITeacherSubjectRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly QuestionBuilder _builder = new();
    private readonly RejectQuestionHandler _handler;

    public RejectQuestionHandlerTests()
    {
        _currentUserService.UserId.Returns(_builder.Teacher.Id);
        _teacherSubjectRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<TeacherSubject, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<TeacherSubject>, IQueryable<TeacherSubject>>?>(), Arg.Any<Func<IQueryable<TeacherSubject>, IOrderedQueryable<TeacherSubject>>?>(), Arg.Any<bool>()).Returns(TeacherSubject.Create(_builder.Teacher, _builder.Subject, Guid.NewGuid()));
        _handler = new RejectQuestionHandler(_questionRepository, _teacherSubjectRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_AssignedTeacher_RejectsWithReasonAndSaves()
    {
        var question = Stub(_builder.Build());

        await _handler.Handle(new RejectQuestionCommand(question.Id, question.Version, "Wrong unit"), TestContext.Current.CancellationToken);

        question.ValidationStatus.Should().Be(QuestionValidationStatus.Rejected);
        question.RejectionReason.Should().Be("Wrong unit");
        question.ValidatedBy.Should().Be(_builder.Teacher.Id);
        question.Decisions.Should().ContainSingle().Which.Outcome.Should().Be(QuestionDecisionOutcome.Rejected);
        await _questionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        var question = Stub(_builder.Build());
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new RejectQuestionCommand(question.Id, question.Version, "Wrong unit"), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _questionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_QuestionMissing_ThrowsQuestionNotFound()
    {
        var act = () => _handler.Handle(new RejectQuestionCommand(Guid.NewGuid(), 1, "Wrong unit"), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.QuestionNotFound);
        await _questionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TeacherNotAssigned_ThrowsSubjectOutOfScope()
    {
        var question = Stub(_builder.Build());
        _teacherSubjectRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<TeacherSubject, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<TeacherSubject>, IQueryable<TeacherSubject>>?>(), Arg.Any<Func<IQueryable<TeacherSubject>, IOrderedQueryable<TeacherSubject>>?>(), Arg.Any<bool>()).Returns((TeacherSubject?)null);

        var act = () => _handler.Handle(new RejectQuestionCommand(question.Id, question.Version, "Wrong unit"), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ForbiddenCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.SubjectOutOfScope);
        question.ValidationStatus.Should().Be(QuestionValidationStatus.Pending);
        await _questionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_StaleVersion_ThrowsQuestionVersionChanged()
    {
        var question = Stub(_builder.Build());

        var act = () => _handler.Handle(new RejectQuestionCommand(question.Id, question.Version + 1, "Wrong unit"), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(DomainErrorCodes.QuestionVersionChanged);
        question.ValidationStatus.Should().Be(QuestionValidationStatus.Pending);
        await _questionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private Question Stub(Question question)
    {
        _questionRepository.GetByIdAsync(question.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Question>, IQueryable<Question>>?>(), Arg.Any<bool>()).Returns(question);
        return question;
    }
}
