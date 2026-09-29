namespace Elmanhg.Application.Browse.Shared;

public sealed record StudentSubjectResult(Guid Id, string Name, int ServableCount, int MasteredCount, int SeenCount, int MasteryPercent, List<StudentUnitSummaryResult> Units);

public sealed record StudentUnitSummaryResult(Guid Id, string Name, int LessonCount, int ServableCount, int MasteredCount, int SeenCount, int MasteryPercent, decimal? BestExamScorePercent);
