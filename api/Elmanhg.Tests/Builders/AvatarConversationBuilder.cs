using Elmanhg.Application.Avatar.Shared;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Domain.Avatar;

namespace Elmanhg.Tests.Builders;

public sealed class AvatarConversationBuilder
{
    public static readonly DateTimeOffset DefaultStartedAt = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly List<(string Question, string Reply)> _exchanges = [];
    private Guid _studentId = Guid.NewGuid();
    private AvatarEntryPoint _entryPoint = AvatarEntryPoint.Global;
    private Guid? _subjectId;
    private Guid? _unitId;
    private Guid? _lessonId;
    private Guid? _sessionId;
    private Guid? _questionId;
    private DateTimeOffset _startedAt = DefaultStartedAt;

    public static AvatarAssistantReply Reply(string text = "رد", decimal cost = 0.0021m) => new(text, "claude-sonnet-5", "v2", 100, 20, cost, "end_turn", 0, AvatarMessageJson.WriteContext(new AiContextBundle(AiChatEntryPoint.Global, null, null, null, null, ["الفيزياء"]), []), "[]");

    public AvatarConversationBuilder ForStudent(Guid studentId)
    {
        _studentId = studentId;
        return this;
    }

    public AvatarConversationBuilder WithEntryPoint(AvatarEntryPoint entryPoint)
    {
        _entryPoint = entryPoint;
        return this;
    }

    public AvatarConversationBuilder WithLesson(Guid? subjectId, Guid? unitId, Guid? lessonId)
    {
        (_subjectId, _unitId, _lessonId) = (subjectId, unitId, lessonId);
        return this;
    }

    public AvatarConversationBuilder WithQuestion(Guid sessionId, Guid questionId)
    {
        (_sessionId, _questionId) = (sessionId, questionId);
        return this;
    }

    public AvatarConversationBuilder StartedAt(DateTimeOffset startedAt)
    {
        _startedAt = startedAt;
        return this;
    }

    public AvatarConversationBuilder WithExchange(string question, string reply)
    {
        _exchanges.Add((question, reply));
        return this;
    }

    public AvatarConversation Build()
    {
        var conversation = AvatarConversation.Start(_studentId, _entryPoint, _subjectId, _unitId, _lessonId, _sessionId, _questionId, _startedAt);
        var at = _startedAt;
        foreach (var (question, reply) in _exchanges)
        {
            at = at.AddMinutes(1);
            conversation.RecordExchange(question, Reply(reply), at, at);
        }

        return conversation;
    }
}
