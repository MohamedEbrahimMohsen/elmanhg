using Elmanhg.Domain.Avatar;

namespace Elmanhg.Application.Avatar.Shared;

public sealed record StudentAvatarConversationResult(Guid Id, AvatarEntryPoint EntryPoint, Guid? SubjectId, string? SubjectName, Guid? LessonId, string? LessonName, Guid? SessionId, Guid? QuestionId, DateTimeOffset StartedAt, DateTimeOffset LastMessageAt, int MessageCount, string FirstQuestion);
