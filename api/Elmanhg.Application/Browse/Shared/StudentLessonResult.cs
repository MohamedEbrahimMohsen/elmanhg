using Elmanhg.Application.Lessons.Shared;

namespace Elmanhg.Application.Browse.Shared;

public sealed record StudentLessonResult(Guid Id, string Name, Guid UnitId, string UnitName, Guid SubjectId, string SubjectName, string Explanation, string Summary, string? VideoUrl, List<LessonObjectiveResult> Objectives, int ServableCount, int MasteredCount, int SeenCount, int MasteryPercent, LessonLinkResult? PreviousLesson, LessonLinkResult? NextLesson);

public sealed record LessonLinkResult(Guid Id, string Name, Guid UnitId, string UnitName);
