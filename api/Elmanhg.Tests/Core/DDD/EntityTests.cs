using Core.DDD.Entities;
using FluentAssertions;

namespace Elmanhg.Tests.Core.DDD;

public sealed class EntityTests
{
    private static readonly DateTimeOffset DeletedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SoftDelete_GivenTime_MarksDeletedAndStampsDeletedAt()
    {
        var entity = new ProbeEntity(Guid.NewGuid());

        entity.SoftDelete(DeletedAt);

        (entity.IsDeleted, entity.DeletedAt).Should().Be((true, (DateTimeOffset?)DeletedAt));
    }

    private sealed class ProbeEntity(Guid id) : Entity(id);
}
