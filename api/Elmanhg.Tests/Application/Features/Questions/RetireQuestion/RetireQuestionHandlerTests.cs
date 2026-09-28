using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.RetireQuestion;
using Elmanhg.Domain.Questions;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.Questions.RetireQuestion;

public sealed class RetireQuestionHandlerTests
{
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly RetireQuestionHandler _handler;

    public RetireQuestionHandlerTests()
    {
        _currentUserService.UserId.Returns(_currentUserId);
        _handler = new RetireQuestionHandler(_questionRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_ExistingQuestion_RetiresAndSaves()
    {
        var question = Stub(new QuestionBuilder().Approved().Build());

        await _handler.Handle(new RetireQuestionCommand(question.Id), TestContext.Current.CancellationToken);

        question.RetiredAt.Should().NotBeNull();
        question.UpdatedBy.Should().Be(_currentUserId);
        await _questionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        var question = Stub(new QuestionBuilder().Build());
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new RetireQuestionCommand(question.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        question.RetiredAt.Should().BeNull();
        await _questionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownQuestion_ThrowsQuestionNotFound()
    {
        var act = () => _handler.Handle(new RetireQuestionCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.QuestionNotFound);
        await _questionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AlreadyRetired_ThrowsQuestionAlreadyRetired()
    {
        var question = Stub(new QuestionBuilder().Retired().Build());

        var act = () => _handler.Handle(new RetireQuestionCommand(question.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BusinessRuleViolationCoreException>()).Which.ErrorCode.Should().Be(DomainErrorCodes.QuestionAlreadyRetired);
        await _questionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private Question Stub(Question question)
    {
        _questionRepository.GetByIdAsync(question.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Question>, IQueryable<Question>>?>(), Arg.Any<bool>()).Returns(question);
        return question;
    }
}
