using Elmanhg.Application.ExamBlueprints.Shared;
using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;

namespace Elmanhg.Application.Exams.Shared;

public static class UnitExamOverviewResultGenerator
{
    public static UnitExamOverviewResult Generate(CurriculumUnit unit, Subject subject, ExamBlueprint? blueprint, IReadOnlyDictionary<QuestionType, int> available, Session? openExam, int unopenedLessonCount)
    {
        var isAvailable = blueprint is not null && ExamBlueprintShortfall.Find(blueprint.GetTypeCounts(), available).Count == 0;
        var inProgressExam = openExam is null ? null : new InProgressExamResult(openExam.Id, openExam.ScopeKey == new UnitExamScope(unit.Id).ToKey());
        return new UnitExamOverviewResult(unit.Id, unit.Name, subject.Id, subject.Name, blueprint is null ? null : Summarize(blueprint, available), isAvailable, inProgressExam, unopenedLessonCount);
    }

    private static ExamBlueprintSummaryResult Summarize(ExamBlueprint blueprint, IReadOnlyDictionary<QuestionType, int> available)
    {
        var typeCounts = blueprint.GetTypeCounts()
            .Select(x => new ExamTypeAvailabilityResult(x.Type, x.Count, available.GetValueOrDefault(x.Type)))
            .ToList();
        var mix = blueprint.GetDifficultyMix();
        var difficultyMix = mix is null ? null : new ExamDifficultyMixResult(mix.EasyPercent, mix.MediumPercent, mix.HardPercent);
        return new ExamBlueprintSummaryResult(blueprint.IsSubjectDefault, blueprint.QuestionCount, typeCounts, difficultyMix, blueprint.TimeLimitMinutes, blueprint.PassMark);
    }
}
