using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Sessions.Exams;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;

namespace Elmanhg.Application.Exams.Shared;

public static class MultiUnitExamOverviewResultGenerator
{
    public static MultiUnitExamOverviewResult Generate(Subject subject, IReadOnlyList<CurriculumUnit> units, IReadOnlyCollection<ExamBlueprint> blueprints, IReadOnlyCollection<ServableQuestionCount> counts, Session? openExam)
    {
        var options = units
            .Select(unit => Option(unit, ExamBlueprintResolution.ForUnit(blueprints, unit), counts))
            .ToList();
        var inProgressExam = openExam is null ? null : new InProgressExamResult(openExam.Id, false);
        return new MultiUnitExamOverviewResult(subject.Id, subject.Name, options, [.. MultiUnitExamSizes.All], inProgressExam);
    }

    private static MultiUnitExamUnitOptionResult Option(CurriculumUnit unit, ExamBlueprint? resolved, IReadOnlyCollection<ServableQuestionCount> counts)
    {
        var servableCount = counts
            .Where(x => x.UnitId == unit.Id)
            .Sum(x => x.Count);
        return new MultiUnitExamUnitOptionResult(unit.Id, unit.Name, resolved is not null, resolved?.IsSubjectDefault ?? false, servableCount);
    }
}
