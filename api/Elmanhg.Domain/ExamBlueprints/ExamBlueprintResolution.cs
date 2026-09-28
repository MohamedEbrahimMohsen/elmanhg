using Elmanhg.Domain.Units;

namespace Elmanhg.Domain.ExamBlueprints;

public static class ExamBlueprintResolution
{
    public static ExamBlueprint? ForUnit(IEnumerable<ExamBlueprint> blueprints, CurriculumUnit unit)
    {
        var list = blueprints.ToList();
        return list.FirstOrDefault(x => x.UnitId == unit.Id) ?? list.FirstOrDefault(x => x.IsSubjectDefault && x.SubjectId == unit.SubjectId);
    }
}
