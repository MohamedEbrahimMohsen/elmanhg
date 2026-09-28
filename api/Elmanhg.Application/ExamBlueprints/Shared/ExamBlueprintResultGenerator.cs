using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;

namespace Elmanhg.Application.ExamBlueprints.Shared;

public static class ExamBlueprintResultGenerator
{
    public static ExamBlueprintResult Generate(ExamBlueprint blueprint)
    {
        var typeCounts = blueprint.GetTypeCounts()
            .Select(x => new ExamTypeCountResult(x.Type, x.Count))
            .ToList();
        var mix = blueprint.GetDifficultyMix();
        var difficultyMix = mix is null ? null : new ExamDifficultyMixResult(mix.EasyPercent, mix.MediumPercent, mix.HardPercent);
        return new ExamBlueprintResult(blueprint.Id, blueprint.SubjectId, blueprint.UnitId, typeCounts, difficultyMix, blueprint.QuestionCount, blueprint.TimeLimitMinutes, blueprint.PassMark);
    }

    public static SubjectExamBlueprintsResult GenerateOverview(Subject subject, List<CurriculumUnit> units, List<ExamBlueprint> blueprints, List<ServableQuestionCount> counts)
    {
        var defaultBlueprint = blueprints.FirstOrDefault(x => x.UnitId == null);
        var unitResults = units
            .Select(unit => GenerateUnit(unit, blueprints.FirstOrDefault(x => x.UnitId == unit.Id), counts))
            .ToList();
        return new SubjectExamBlueprintsResult(subject.Id, subject.Name, defaultBlueprint is null ? null : Generate(defaultBlueprint), ServableTypeCounts.ToResults(ServableTypeCounts.ForSubject(counts)), unitResults);
    }

    private static UnitExamBlueprintResult GenerateUnit(CurriculumUnit unit, ExamBlueprint? blueprint, List<ServableQuestionCount> counts)
    {
        return new UnitExamBlueprintResult(unit.Id, unit.Name, unit.Order, blueprint is null ? null : Generate(blueprint), ServableTypeCounts.ToResults(ServableTypeCounts.ForUnit(counts, unit.Id)));
    }
}
