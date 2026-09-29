namespace Elmanhg.Application.Mastery.Shared;

public sealed record SubjectMasteryResult(Guid SubjectId, string Name, int ServableCount, int MasteredCount, int SeenCount, int MasteryPercent, bool IsInterested);
