using Elmanhg.Domain.Avatar;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Avatar;

public sealed class AvatarConversationTests
{
    private static readonly DateTimeOffset StartedAt = AvatarConversationBuilder.DefaultStartedAt;
    private static readonly Guid LessonId = Guid.NewGuid();
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid QuestionId = Guid.NewGuid();
    private readonly Guid _studentId = Guid.NewGuid();

    [Fact]
    public void Start_ValidInput_SetsScopeAndEmptyLog()
    {
        var subjectId = Guid.NewGuid();
        var unitId = Guid.NewGuid();

        var conversation = AvatarConversation.Start(_studentId, AvatarEntryPoint.QuizQuestion, subjectId, unitId, LessonId, SessionId, QuestionId, StartedAt);

        (conversation.StudentId, conversation.EntryPoint, conversation.CreatedBy).Should().Be((_studentId, AvatarEntryPoint.QuizQuestion, (Guid?)_studentId));
        (conversation.SubjectId, conversation.UnitId, conversation.LessonId, conversation.SessionId, conversation.QuestionId).Should().Be(((Guid?)subjectId, (Guid?)unitId, (Guid?)LessonId, (Guid?)SessionId, (Guid?)QuestionId));
        (conversation.StartedAt, conversation.LastMessageAt).Should().Be((StartedAt, StartedAt));
        conversation.MessageCount.Should().Be(0);
        conversation.Messages.Should().BeEmpty();
    }

    [Fact]
    public void Start_SubMicrosecondTimestamp_TruncatesToMicroseconds()
    {
        var conversation = AvatarConversation.Start(_studentId, AvatarEntryPoint.Global, null, null, null, null, null, StartedAt.AddTicks(7));

        (conversation.StartedAt.Ticks % 10).Should().Be(0);
        conversation.StartedAt.Should().Be(StartedAt);
    }

    [Fact]
    public void RecordExchange_First_AppendsStudentThenAssistantAtPositions0And1()
    {
        var conversation = AvatarConversation.Start(_studentId, AvatarEntryPoint.Global, null, null, null, null, null, StartedAt);
        var reply = AvatarConversationBuilder.Reply("R = V / I");
        var askedAt = StartedAt.AddSeconds(1);
        var repliedAt = StartedAt.AddSeconds(5);

        conversation.RecordExchange("  What is resistance?  ", reply, askedAt, repliedAt);

        conversation.Messages.Should().HaveCount(2);
        var student = conversation.Messages[0];
        var assistant = conversation.Messages[1];
        (student.ConversationId, student.Position, student.Role, student.Text, student.CreatedAt).Should().Be((conversation.Id, 0, AvatarMessageRole.Student, "What is resistance?", askedAt));
        (student.Model, student.PromptVersion, student.InputTokens, student.OutputTokens, student.CostUsd, student.StopReason, student.HistoryMessageCount, student.Context, student.Citations).Should().Be(((string?)null, (string?)null, (int?)null, (int?)null, (decimal?)null, (string?)null, (int?)null, (string?)null, (string?)null));
        (assistant.ConversationId, assistant.Position, assistant.Role, assistant.Text, assistant.CreatedAt).Should().Be((conversation.Id, 1, AvatarMessageRole.Assistant, "R = V / I", repliedAt));
        (assistant.Model, assistant.PromptVersion, assistant.InputTokens, assistant.OutputTokens, assistant.CostUsd, assistant.StopReason).Should().Be(((string?)reply.Model, (string?)reply.PromptVersion, (int?)reply.InputTokens, (int?)reply.OutputTokens, (decimal?)reply.CostUsd, reply.StopReason));
        (assistant.HistoryMessageCount, assistant.Context, assistant.Citations).Should().Be(((int?)reply.HistoryMessageCount, (string?)reply.Context, (string?)reply.Citations));
    }

    [Fact]
    public void RecordExchange_Second_ContinuesPositionsAndStampsConversation()
    {
        var conversation = new AvatarConversationBuilder().ForStudent(_studentId).WithExchange("q1", "r1").Build();
        conversation.UpdatedBy = null;
        var repliedAt = StartedAt.AddHours(1);

        conversation.RecordExchange("q2", AvatarConversationBuilder.Reply("r2"), repliedAt.AddSeconds(-3), repliedAt);

        conversation.Messages.Skip(2).Select(x => x.Position).Should().Equal(2, 3);
        conversation.MessageCount.Should().Be(4);
        (conversation.LastMessageAt, conversation.UpdationDate, conversation.UpdatedBy).Should().Be((repliedAt, repliedAt, (Guid?)null));
    }

    [Fact]
    public void RecentMessages_MoreThanCount_ReturnsLastInPositionOrder()
    {
        var conversation = new AvatarConversationBuilder().WithExchange("q1", "r1").WithExchange("q2", "r2").WithExchange("q3", "r3").Build();

        var recent = conversation.RecentMessages(4);

        recent.Select(x => x.Position).Should().Equal(2, 3, 4, 5);
    }

    [Fact]
    public void RecentMessages_ZeroCount_ReturnsEmpty()
    {
        var conversation = new AvatarConversationBuilder().WithExchange("q1", "r1").Build();

        conversation.RecentMessages(0).Should().BeEmpty();
    }

    [Fact]
    public void Delete_Conversation_SoftDeletesAndZeroesMessageCount()
    {
        var conversation = new AvatarConversationBuilder().ForStudent(_studentId).WithExchange("q1", "r1").Build();
        conversation.UpdatedBy = null;
        var deletedAt = StartedAt.AddDays(1);

        conversation.Delete(deletedAt.AddTicks(7));

        (conversation.IsDeleted, conversation.MessageCount).Should().Be((true, 0));
        (conversation.DeletedAt, conversation.UpdationDate, conversation.UpdatedBy).Should().Be(((DateTimeOffset?)deletedAt, deletedAt, (Guid?)null));
    }

    [Fact]
    public void Delete_Conversation_KeepsContextAndTimes()
    {
        var conversation = AvatarConversation.Start(_studentId, AvatarEntryPoint.Lesson, null, null, LessonId, null, null, StartedAt);
        conversation.RecordExchange("q1", AvatarConversationBuilder.Reply("r1"), StartedAt.AddMinutes(1), StartedAt.AddMinutes(2));

        conversation.Delete(StartedAt.AddDays(1));

        (conversation.StudentId, conversation.EntryPoint, conversation.LessonId).Should().Be((_studentId, AvatarEntryPoint.Lesson, (Guid?)LessonId));
        (conversation.StartedAt, conversation.LastMessageAt).Should().Be((StartedAt, StartedAt.AddMinutes(2)));
    }

    [Theory]
    [InlineData(AvatarEntryPoint.Lesson, AvatarEntryPoint.Lesson, true, true, true, true)]
    [InlineData(AvatarEntryPoint.Lesson, AvatarEntryPoint.Lesson, false, true, true, false)]
    [InlineData(AvatarEntryPoint.QuizQuestion, AvatarEntryPoint.QuizQuestion, true, true, true, true)]
    [InlineData(AvatarEntryPoint.QuizQuestion, AvatarEntryPoint.QuizQuestion, true, true, false, false)]
    [InlineData(AvatarEntryPoint.ExamReview, AvatarEntryPoint.ExamReview, false, true, true, true)]
    [InlineData(AvatarEntryPoint.Global, AvatarEntryPoint.Global, false, false, false, true)]
    [InlineData(AvatarEntryPoint.Global, AvatarEntryPoint.Lesson, true, true, true, false)]
    public void IsFor_ScopeCases_MatchesOnlyTheSameContext(AvatarEntryPoint started, AvatarEntryPoint asked, bool sameLesson, bool sameSession, bool sameQuestion, bool expected)
    {
        var conversation = AvatarConversation.Start(_studentId, started, null, null, LessonId, SessionId, QuestionId, StartedAt);

        var result = conversation.IsFor(asked, sameLesson ? LessonId : Guid.NewGuid(), sameSession ? SessionId : Guid.NewGuid(), sameQuestion ? QuestionId : Guid.NewGuid());

        result.Should().Be(expected);
    }
}
