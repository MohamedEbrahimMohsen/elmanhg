namespace Elmanhg.Application.Progress.Shared;

public sealed record UnitProgressResult(Guid UnitId, string Name, int ServableCount, int MasteredCount, int SeenCount, int MasteryPercent, decimal? BestExamScorePercent);
