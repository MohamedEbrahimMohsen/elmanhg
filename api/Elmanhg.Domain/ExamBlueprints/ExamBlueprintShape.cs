namespace Elmanhg.Domain.ExamBlueprints;

public sealed record ExamBlueprintShape(IReadOnlyList<ExamTypeCount> TypeCounts, ExamDifficultyMix? DifficultyMix, int? TimeLimitMinutes, int PassMark);
