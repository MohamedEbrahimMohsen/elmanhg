namespace Elmanhg.Application.Mastery.Shared;

public sealed record SubjectMasteryDetailResult(Guid SubjectId, string Name, int ServableCount, int MasteredCount, int SeenCount, int MasteryPercent, List<UnitMasteryResult> Units);
