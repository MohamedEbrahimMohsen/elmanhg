using Elmanhg.Application.Browse.Shared;
using MediatR;

namespace Elmanhg.Application.Browse.GetStudentSubject;

public sealed record GetStudentSubjectQuery(Guid SubjectId) : IRequest<StudentSubjectResult>;
