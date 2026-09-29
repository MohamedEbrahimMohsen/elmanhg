namespace Elmanhg.Application.Browse.Shared;

public sealed record StudentUnitResult(Guid Id, string Name, Guid SubjectId, string SubjectName, int ServableCount, int MasteredCount, int SeenCount, int MasteryPercent, decimal? BestExamScorePercent, List<StudentLessonSummaryResult> Lessons);

public sealed record StudentLessonSummaryResult(Guid Id, string Name, int ServableCount, int MasteredCount, int SeenCount, int MasteryPercent, bool IsLocked);
