using Elmanhg.Application.Units.Shared;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;

namespace Elmanhg.Application.Subjects.Shared;

public static class SubjectResultGenerator
{
    public static SubjectResult Generate(Subject subject, int unitCount)
    {
        return new SubjectResult(subject.Id, subject.Name, subject.Order, unitCount);
    }

    public static SubjectDetailResult GenerateDetail(Subject subject, List<CurriculumUnit> units, Dictionary<Guid, int> lessonCounts)
    {
        var unitResults = units
            .Select(x => UnitResultGenerator.Generate(x, lessonCounts.GetValueOrDefault(x.Id)))
            .ToList();

        return new SubjectDetailResult(subject.Id, subject.Name, subject.Order, unitResults);
    }
}
