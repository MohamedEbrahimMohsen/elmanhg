using Elmanhg.Domain.Avatar;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Avatar;

public sealed class AvatarExchangeRecordedEventTests
{
    [Fact]
    public void RecordExchange_Always_RaisesEventWithBothMessages()
    {
        var conversation = AvatarConversation.Start(Guid.NewGuid(), AvatarEntryPoint.Global, null, null, null, null, null, AvatarConversationBuilder.DefaultStartedAt);

        conversation.RecordExchange("question", AvatarConversationBuilder.Reply("reply"), AvatarConversationBuilder.DefaultStartedAt.AddMinutes(1), AvatarConversationBuilder.DefaultStartedAt.AddMinutes(1));

        var recorded = conversation.GetDomainEvents().OfType<AvatarExchangeRecorded>().Should().ContainSingle().Subject;
        recorded.Conversation.Should().BeSameAs(conversation);
        recorded.StudentMessage.Should().BeSameAs(conversation.Messages[0]);
        recorded.AssistantMessage.Should().BeSameAs(conversation.Messages[1]);
    }
}
