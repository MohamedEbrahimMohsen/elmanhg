using Elmanhg.Domain.Avatar;

namespace Elmanhg.Application.Avatar.Shared;

public sealed record AdminAvatarConversationDetailResult(Guid Id, Guid StudentId, string StudentName, AvatarEntryPoint EntryPoint, Guid? SubjectId, string? SubjectName, Guid? UnitId, string? UnitName, Guid? LessonId, string? LessonName, Guid? SessionId, Guid? QuestionId, DateTimeOffset StartedAt, DateTimeOffset LastMessageAt, int MessageCount, int TotalInputTokens, int TotalOutputTokens, decimal? TotalCostUsd, List<AdminAvatarMessageResult> Messages);
