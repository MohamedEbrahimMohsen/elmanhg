using Elmanhg.Application.Students.Shared;
using MediatR;

namespace Elmanhg.Application.Students.GetStudentProfile;

public sealed record GetStudentProfileQuery(Guid StudentId) : IRequest<StudentProfileResult>;
