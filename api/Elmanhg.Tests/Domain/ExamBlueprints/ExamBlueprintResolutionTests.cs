using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Units;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.ExamBlueprints;

public sealed class ExamBlueprintResolutionTests
{
    private readonly ExamBlueprintBuilder _builder = new();

    [Fact]
    public void ForUnit_UnitBlueprintExists_ReturnsIt()
    {
        var unitBlueprint = _builder.BuildForUnit();

        var result = ExamBlueprintResolution.ForUnit([_builder.BuildDefault(), unitBlueprint], _builder.Unit);

        result.Should().BeSameAs(unitBlueprint);
    }

    [Fact]
    public void ForUnit_NoUnitBlueprint_ReturnsSubjectDefault()
    {
        var defaultBlueprint = _builder.BuildDefault();

        var result = ExamBlueprintResolution.ForUnit([defaultBlueprint], _builder.Unit);

        result.Should().BeSameAs(defaultBlueprint);
    }

    [Fact]
    public void ForUnit_NoBlueprints_ReturnsNull()
    {
        ExamBlueprintResolution.ForUnit([], _builder.Unit).Should().BeNull();
    }

    [Fact]
    public void ForUnit_OtherUnitsBlueprint_IsIgnored()
    {
        var otherUnit = CurriculumUnit.Create(_builder.Subject, "Optics", 2, Guid.NewGuid());
        var otherBlueprint = ExamBlueprint.CreateForUnit(otherUnit, ExamBlueprintBuilder.Shape(new ExamTypeCount(QuestionType.Mcq, 1)), ExamBlueprintBuilder.Plenty(), Guid.NewGuid());
        var defaultBlueprint = _builder.BuildDefault();

        ExamBlueprintResolution.ForUnit([otherBlueprint], _builder.Unit).Should().BeNull();
        ExamBlueprintResolution.ForUnit([otherBlueprint, defaultBlueprint], _builder.Unit).Should().BeSameAs(defaultBlueprint);
    }
}
