using Elmanhg.Domain.TeacherThreads;

namespace Elmanhg.Application.TeacherThreads.Shared;

public sealed record TeacherMessageResult(Guid Id, bool IsFromStudent, TeacherMessageKind Kind, string Text, string? ImageUrl, DateTimeOffset CreatedAt);
