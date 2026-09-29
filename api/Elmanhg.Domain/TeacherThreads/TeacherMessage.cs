using Core.DDD.Entities;
using Core.Errors;
using Elmanhg.Domain.SharedKernel.Exceptions;

namespace Elmanhg.Domain.TeacherThreads;

public class TeacherMessage : Entity
{
    public Guid ThreadId { get; private set; }
    public Guid SenderId { get; private set; }
    public TeacherMessageKind Kind { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public string? ImageUrl { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? StudentReadAt { get; private set; }

    private TeacherMessage(Guid id) : base(id) { }

    internal static TeacherMessage CreateText(Guid threadId, Guid senderId, string text, string? imageUrl, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.TeacherMessageTextRequired);
        }

        return new TeacherMessage(Guid.NewGuid())
        {
            ThreadId = threadId,
            SenderId = senderId,
            Kind = TeacherMessageKind.Text,
            Text = text.Trim(),
            ImageUrl = imageUrl,
            CreatedAt = createdAt,
        };
    }

    internal void MarkReadByStudent(DateTimeOffset readAt) => StudentReadAt ??= readAt;
}
