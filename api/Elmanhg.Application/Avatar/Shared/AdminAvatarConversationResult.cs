using Elmanhg.Domain.Avatar;

namespace Elmanhg.Application.Avatar.Shared;

public sealed record AdminAvatarConversationResult(Guid Id, Guid StudentId, string StudentName, AvatarEntryPoint EntryPoint, Guid? SubjectId, string? SubjectName, Guid? LessonId, string? LessonName, Guid? QuestionId, DateTimeOffset StartedAt, DateTimeOffset LastMessageAt, int MessageCount, string FirstQuestion);
