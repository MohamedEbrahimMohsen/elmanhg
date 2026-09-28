using Elmanhg.Application.Lessons.Shared;
using MediatR;

namespace Elmanhg.Application.Lessons.GetLesson;

public sealed record GetLessonQuery(Guid LessonId) : IRequest<LessonDetailResult>;
