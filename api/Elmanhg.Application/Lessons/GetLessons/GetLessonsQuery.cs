using Elmanhg.Application.Lessons.Shared;
using MediatR;

namespace Elmanhg.Application.Lessons.GetLessons;

public sealed record GetLessonsQuery(Guid UnitId) : IRequest<List<LessonResult>>;
