using Elmanhg.Application.Browse.Shared;
using MediatR;

namespace Elmanhg.Application.Browse.GetStudentLesson;

public sealed record GetStudentLessonQuery(Guid LessonId) : IRequest<StudentLessonResult>;
