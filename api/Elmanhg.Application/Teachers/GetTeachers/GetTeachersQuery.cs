using Elmanhg.Application.Teachers.Shared;
using MediatR;

namespace Elmanhg.Application.Teachers.GetTeachers;

public sealed record GetTeachersQuery : IRequest<List<TeacherResult>>;
