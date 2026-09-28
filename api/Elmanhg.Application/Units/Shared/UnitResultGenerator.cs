using Elmanhg.Domain.Units;

namespace Elmanhg.Application.Units.Shared;

public static class UnitResultGenerator
{
    public static UnitResult Generate(CurriculumUnit unit)
    {
        return new UnitResult(unit.Id, unit.SubjectId, unit.Name, unit.Order);
    }
}
