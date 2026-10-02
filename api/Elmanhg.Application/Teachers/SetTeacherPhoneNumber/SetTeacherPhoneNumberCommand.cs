using Core.Auditing;
using MediatR;

namespace Elmanhg.Application.Teachers.SetTeacherPhoneNumber;

public sealed record SetTeacherPhoneNumberCommand(Guid TeacherId, string? PhoneNumber) : IRequest, IAuditableCommand
{
    public string AuditAction => "Teacher.SetPhoneNumber";
    public string AuditResourceType => "Teacher";
    public Guid? AuditResourceId => TeacherId;
}
