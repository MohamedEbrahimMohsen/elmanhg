using Elmanhg.Domain.Avatar;
using Elmanhg.Domain.TrainingData;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.TrainingData;

public sealed class AvatarTrainingRecordTests
{
    private const string StudentHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly DateTimeOffset RecordedAt = new(2026, 10, 2, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void From_Exchange_CopiesTextsModelPromptContextAndReferences()
    {
        var (subjectId, unitId, lessonId) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var conversation = new AvatarConversationBuilder().WithEntryPoint(AvatarEntryPoint.Lesson).WithLesson(subjectId, unitId, lessonId).WithExchange("ما هو قانون أوم؟", "V = I R").Build();
        var (student, assistant) = (conversation.Messages[0], conversation.Messages[1]);

        var record = AvatarTrainingRecord.From(conversation, student, assistant, StudentHash, RecordedAt);

        (record.StudentHash, record.ConversationId, record.StudentMessageId, record.AssistantMessageId, record.StudentMessagePosition).Should().Be((StudentHash, conversation.Id, student.Id, assistant.Id, 0));
        (record.StudentText, record.AssistantText, record.Model, record.PromptVersion).Should().Be(("ما هو قانون أوم؟", "V = I R", "claude-sonnet-5", "v2"));
        record.Context.Should().Be(assistant.Context);
        (record.EntryPoint, record.SubjectId, record.UnitId, record.LessonId, record.QuestionId).Should().Be((AvatarEntryPoint.Lesson, (Guid?)subjectId, (Guid?)unitId, (Guid?)lessonId, (Guid?)null));
        (record.AskedAt, record.OccurredAt, record.RecordedAt).Should().Be((student.CreatedAt, assistant.CreatedAt, RecordedAt));
    }

    [Fact]
    public void From_MessagesInSwappedRoles_ThrowsInvalidOperationException()
    {
        var conversation = new AvatarConversationBuilder().WithExchange("question", "reply").Build();

        var act = () => AvatarTrainingRecord.From(conversation, conversation.Messages[1], conversation.Messages[0], StudentHash, RecordedAt);

        act.Should().Throw<InvalidOperationException>().WithMessage("An exchange is a student message followed by an assistant reply.");
    }

    [Fact]
    public void From_MessageOfOtherConversation_ThrowsInvalidOperationException()
    {
        var conversation = new AvatarConversationBuilder().WithExchange("question", "reply").Build();
        var other = new AvatarConversationBuilder().WithExchange("question", "reply").Build();

        var act = () => AvatarTrainingRecord.From(conversation, other.Messages[0], other.Messages[1], StudentHash, RecordedAt);

        act.Should().Throw<InvalidOperationException>().WithMessage("Messages do not belong to the conversation.");
    }
}
