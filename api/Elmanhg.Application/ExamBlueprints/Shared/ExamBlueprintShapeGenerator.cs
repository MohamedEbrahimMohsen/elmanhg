using Elmanhg.Domain.ExamBlueprints;

namespace Elmanhg.Application.ExamBlueprints.Shared;

public static class ExamBlueprintShapeGenerator
{
    public static ExamBlueprintShape Generate(ExamBlueprintInput input)
    {
        var typeCounts = input.TypeCounts
            .Select(x => new ExamTypeCount(x.Type.GetValueOrDefault(), x.Count))
            .ToList();
        var difficultyMix = input.DifficultyMix is null ? null : new ExamDifficultyMix(input.DifficultyMix.EasyPercent, input.DifficultyMix.MediumPercent, input.DifficultyMix.HardPercent);
        return new ExamBlueprintShape(typeCounts, difficultyMix, input.TimeLimitMinutes, input.PassMark);
    }
}
