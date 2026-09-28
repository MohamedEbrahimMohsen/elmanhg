namespace Elmanhg.Application.Progress.Shared;

public sealed record SubjectProgressResult(Guid SubjectId, string Name, int ServableCount, int MasteredCount, int SeenCount, int MasteryPercent, List<UnitProgressResult> Units);
