using Core.DDD.Entities;

namespace Elmanhg.Domain.SlaCalendars;

public class ExamPeriod : AuditEntity, IAuditedEntity, IVersioned
{
    public string Name { get; private set; } = default!;
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public uint Version { get; private set; }

    private ExamPeriod(Guid id, Guid? createdBy) : base(id, createdBy) { }

    public static ExamPeriod Create(string name, DateOnly startDate, DateOnly endDate, Guid createdBy)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(endDate, startDate);
        return new ExamPeriod(Guid.NewGuid(), createdBy) { Name = name, StartDate = startDate, EndDate = endDate };
    }

    public void Update(string name, DateOnly startDate, DateOnly endDate, Guid updatedBy)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(endDate, startDate);
        Name = name;
        StartDate = startDate;
        EndDate = endDate;
        UpdatedBy = updatedBy;
        UpdationDate = DateTimeOffset.UtcNow;
    }

    public void Delete(Guid deletedBy)
    {
        var now = DateTimeOffset.UtcNow;
        SoftDelete(now);
        UpdatedBy = deletedBy;
        UpdationDate = now;
    }

    public SlaDateRange ToDateRange() => new(StartDate, EndDate);
}
