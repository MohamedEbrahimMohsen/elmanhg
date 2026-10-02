using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Avatar.DeleteMyAvatarConversation;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.RuntimeSettings.Definitions;
using Elmanhg.Domain.Avatar;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Fixtures.RuntimeSettings;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Avatar.DeleteMyAvatarConversation;

public sealed class DeleteMyAvatarConversationHandlerTests
{
    private static readonly DateTimeOffset Now = AvatarConversationBuilder.DefaultStartedAt.AddDays(1);
    private readonly IAvatarConversationRepository _conversations = Substitute.For<IAvatarConversationRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _studentId = Guid.NewGuid();
    private FakeRuntimeSettings _runtimeSettings = new();

    public DeleteMyAvatarConversationHandlerTests()
    {
        _currentUserService.UserId.Returns(_studentId);
        _timeProvider.GetUtcNow().Returns(Now);
        _conversations.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task>>()(call.Arg<CancellationToken>()));
        AvatarTestData.StubConversations(_conversations);
    }

    [Fact]
    public async Task Handle_Own_ErasesSoftDeletesAndSaves()
    {
        var conversation = Conversation(_studentId);
        AvatarTestData.StubConversations(_conversations, conversation);

        await Handle(conversation.Id);

        await _conversations.Received(1).EraseMessagesAsync(conversation.Id, Arg.Any<CancellationToken>());
        (conversation.IsDeleted, conversation.MessageCount, conversation.DeletedAt).Should().Be((true, 0, (DateTimeOffset?)Now));
        await _conversations.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _conversations.Received(1).ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Own_ErasesBeforeSaving()
    {
        var conversation = Conversation(_studentId);
        AvatarTestData.StubConversations(_conversations, conversation);

        await Handle(conversation.Id);

        Received.InOrder(() =>
        {
            _conversations.EraseMessagesAsync(conversation.Id, Arg.Any<CancellationToken>());
            _conversations.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUnauthorized()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => Handle(Guid.NewGuid());

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await AssertNothingErasedOrSaved(transactionStarted: false);
    }

    [Fact]
    public async Task Handle_DeletionDisabled_ThrowsDeletionDisabled()
    {
        var conversation = Conversation(_studentId);
        AvatarTestData.StubConversations(_conversations, conversation);
        _runtimeSettings = new FakeRuntimeSettings().Set(FeatureFlagRuntimeSettings.StudentsCanDeleteAvatarChats, false);

        var act = () => Handle(conversation.Id);

        (await act.Should().ThrowAsync<BusinessRuleViolationCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.AvatarConversationDeletionDisabled);
        await AssertNothingErasedOrSaved(transactionStarted: false);
        conversation.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_OtherStudentsConversation_ThrowsNotFound()
    {
        var conversation = Conversation(Guid.NewGuid());
        AvatarTestData.StubConversations(_conversations, conversation);

        var act = () => Handle(conversation.Id);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.AvatarConversationNotFound);
        await AssertNothingErasedOrSaved(transactionStarted: true);
        (conversation.IsDeleted, conversation.MessageCount).Should().Be((false, 2));
    }

    [Fact]
    public async Task Handle_Unknown_ThrowsNotFound()
    {
        AvatarTestData.StubConversations(_conversations, Conversation(_studentId));

        var act = () => Handle(Guid.NewGuid());

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.AvatarConversationNotFound);
        await AssertNothingErasedOrSaved(transactionStarted: true);
    }

    private static AvatarConversation Conversation(Guid studentId) => new AvatarConversationBuilder().ForStudent(studentId).WithExchange("q1", "r1").Build();

    private async Task AssertNothingErasedOrSaved(bool transactionStarted)
    {
        await _conversations.Received(transactionStarted ? 1 : 0).ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>());
        await _conversations.DidNotReceive().EraseMessagesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _conversations.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private Task Handle(Guid conversationId) => new DeleteMyAvatarConversationHandler(_conversations, _runtimeSettings, _timeProvider, _currentUserService).Handle(new DeleteMyAvatarConversationCommand(conversationId), TestContext.Current.CancellationToken);
}
