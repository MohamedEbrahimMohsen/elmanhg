using Core.DDD.Entities;
using Core.Errors;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Domain.Subjects;

namespace Elmanhg.Domain.Teachers;

public class TeacherSubject : AuditEntity
{
    public Guid TeacherId { get; private set; }
    public Guid SubjectId { get; private set; }

    private TeacherSubject(Guid id, Guid? createdBy) : base(id, createdBy) { }

    public static TeacherSubject Create(User teacher, Subject subject, Guid createdBy)
    {
        if (teacher.Role != UserRole.Teacher)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.UserNotTeacher);
        }

        return new TeacherSubject(Guid.NewGuid(), createdBy)
        {
            TeacherId = teacher.Id,
            SubjectId = subject.Id,
        };
    }

    public void Unassign(Guid unassignedBy)
    {
        SoftDelete();
        UpdatedBy = unassignedBy;
        UpdationDate = DateTimeOffset.UtcNow;
    }
}
