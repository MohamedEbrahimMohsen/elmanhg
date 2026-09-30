namespace Elmanhg.Application.Progress.Shared;

public sealed record StudentProgressResult(List<SubjectProgressResult> Subjects, WeakSpotsResult WeakSpots);
