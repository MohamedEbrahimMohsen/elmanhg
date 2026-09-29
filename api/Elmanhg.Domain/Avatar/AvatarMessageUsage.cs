using Core.DDD.Entities;

namespace Elmanhg.Domain.Avatar;

public class AvatarMessageUsage : Entity
{
    public Guid StudentId { get; private set; }
    public AvatarEntryPoint EntryPoint { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private AvatarMessageUsage(Guid id) : base(id) { }

    public static AvatarMessageUsage Record(Guid studentId, AvatarEntryPoint entryPoint, DateTimeOffset createdAt)
    {
        return new AvatarMessageUsage(Guid.NewGuid())
        {
            StudentId = studentId,
            EntryPoint = entryPoint,
            CreatedAt = createdAt,
        };
    }
}
