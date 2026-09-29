using Elmanhg.Application.Avatar.GetAvatarConversations;
using Elmanhg.Domain.Avatar;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Avatar.GetAvatarConversations;

public sealed class GetAvatarConversationsFilterTests
{
    private static readonly DateTimeOffset Boundary = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Build_NoFilters_MatchesAll()
    {
        List<AvatarConversation> conversations = [Conversation(), Conversation(AvatarEntryPoint.Lesson)];

        Apply(Query(), conversations).Should().HaveCount(2);
    }

    [Fact]
    public void Build_EntryPoint_MatchesOnlyThatEntryPoint()
    {
        var lesson = Conversation(AvatarEntryPoint.Lesson);

        var matched = Apply(Query() with { EntryPoint = AvatarEntryPoint.Lesson }, [Conversation(), lesson]);

        matched.Should().ContainSingle().Which.Should().BeSameAs(lesson);
    }

    [Fact]
    public void Build_DateRange_FromInclusiveToExclusiveOnLastMessageAt()
    {
        var before = Conversation(lastMessageAt: Boundary.AddMicroseconds(-1));
        var atFrom = Conversation(lastMessageAt: Boundary);
        var beforeTo = Conversation(lastMessageAt: Boundary.AddDays(1).AddMicroseconds(-1));
        var atTo = Conversation(lastMessageAt: Boundary.AddDays(1));

        var matched = Apply(Query() with { From = Boundary, To = Boundary.AddDays(1) }, [before, atFrom, beforeTo, atTo]);

        matched.Should().Equal(atFrom, beforeTo);
    }

    [Fact]
    public void Build_Search_MatchesMessageTextCaseInsensitively()
    {
        var ohm = Conversation(reply: "It follows ohm's law");

        var matched = Apply(Query() with { Search = "OHM" }, [Conversation(reply: "Newton"), ohm]);

        matched.Should().ContainSingle().Which.Should().BeSameAs(ohm);
    }

    [Fact]
    public void Build_Search_MatchesListedStudentIds()
    {
        var studentId = Guid.NewGuid();
        var listed = new AvatarConversationBuilder().ForStudent(studentId).WithExchange("q", "r").Build();

        var matched = GetAvatarConversationsFilter.Build(Query() with { Search = "sara" }, [studentId]).Compile();

        matched(listed).Should().BeTrue();
        matched(Conversation()).Should().BeFalse();
    }

    [Fact]
    public void Term_BlankSearch_ReturnsNull()
    {
        GetAvatarConversationsFilter.Term(Query() with { Search = "  " }).Should().BeNull();
        GetAvatarConversationsFilter.Term(Query() with { Search = " Ohm " }).Should().Be("ohm");
    }

    private static GetAvatarConversationsQuery Query() => new(null, null, null, null);

    private static List<AvatarConversation> Apply(GetAvatarConversationsQuery query, List<AvatarConversation> conversations) => conversations.Where(GetAvatarConversationsFilter.Build(query, []).Compile()).ToList();

    private static AvatarConversation Conversation(AvatarEntryPoint entryPoint = AvatarEntryPoint.Global, DateTimeOffset? lastMessageAt = null, string reply = "reply")
    {
        var conversation = new AvatarConversationBuilder().WithEntryPoint(entryPoint).Build();
        var at = lastMessageAt ?? Boundary;
        conversation.RecordExchange("question", AvatarConversationBuilder.Reply(reply), at, at);
        return conversation;
    }
}
