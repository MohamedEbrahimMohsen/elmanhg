using Elmanhg.Application.TeacherThreads.Shared;
using MediatR;

namespace Elmanhg.Application.TeacherThreads.GetTeacherThreadContext;

public sealed record GetTeacherThreadContextQuery(Guid? LessonId, Guid? QuestionId, Guid? AttemptId) : IRequest<TeacherThreadContextResult>;
