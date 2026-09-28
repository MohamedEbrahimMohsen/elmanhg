using Core.Errors;
using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.ExamBlueprints;

public sealed class ExamBlueprintTests
{
    private static readonly DateTimeOffset LongAgo = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly ExamBlueprintBuilder _builder = new();

    [Fact]
    public void CreateForSubject_EnoughServable_StoresCanonicalShape()
    {
        var shape = ExamBlueprintBuilder.Shape(new ExamTypeCount(QuestionType.Fill, 1), new ExamTypeCount(QuestionType.Short, 0), new ExamTypeCount(QuestionType.Mcq, 2));

        var blueprint = ExamBlueprint.CreateForSubject(_builder.Subject, shape, ExamBlueprintBuilder.Plenty(), _builder.CreatedBy);

        blueprint.TypeCounts.Should().Be("[{\"type\":\"mcq\",\"count\":2},{\"type\":\"fill\",\"count\":1}]");
        blueprint.QuestionCount.Should().Be(3);
        blueprint.UnitId.Should().BeNull();
        blueprint.IsSubjectDefault.Should().BeTrue();
        blueprint.SubjectId.Should().Be(_builder.Subject.Id);
        blueprint.PassMark.Should().Be(50);
        blueprint.TimeLimitMinutes.Should().Be(45);
        blueprint.CreatedBy.Should().Be(_builder.CreatedBy);
    }

    [Fact]
    public void CreateForUnit_EnoughServable_SetsSubjectAndUnit()
    {
        var blueprint = _builder.BuildForUnit();

        blueprint.SubjectId.Should().Be(_builder.Unit.SubjectId);
        blueprint.UnitId.Should().Be(_builder.Unit.Id);
        blueprint.IsSubjectDefault.Should().BeFalse();
    }

    [Fact]
    public void CreateForSubject_Shortfall_ThrowsExamBlueprintShortfall()
    {
        var servable = new Dictionary<QuestionType, int> { [QuestionType.Mcq] = 1 };

        var act = () => ExamBlueprint.CreateForSubject(_builder.Subject, ExamBlueprintBuilder.Shape(new ExamTypeCount(QuestionType.Mcq, 2)), servable, _builder.CreatedBy);

        var exception = act.Should().Throw<BusinessRuleViolationCoreException>().Which;
        exception.ErrorCode.Should().Be(ErrorCodes.ExamBlueprintShortfall);
        exception.Context!["types"].Should().Be("Mcq 1/2");
    }

    [Fact]
    public void CreateForUnit_Shortfall_ThrowsExamBlueprintShortfall()
    {
        var act = () => ExamBlueprint.CreateForUnit(_builder.Unit, ExamBlueprintBuilder.Shape(new ExamTypeCount(QuestionType.Short, 1)), new Dictionary<QuestionType, int>(), _builder.CreatedBy);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.ExamBlueprintShortfall);
    }

    [Fact]
    public void Update_EnoughServable_ReplacesShapeAndStamps()
    {
        var blueprint = ExamBlueprint.CreateForSubject(_builder.Subject, new ExamBlueprintShape([new ExamTypeCount(QuestionType.Mcq, 2)], new ExamDifficultyMix(30, 50, 20), 45, 50), ExamBlueprintBuilder.Plenty(), _builder.CreatedBy);
        blueprint.UpdationDate = LongAgo;
        var updatedBy = Guid.NewGuid();

        blueprint.Update(new ExamBlueprintShape([new ExamTypeCount(QuestionType.TrueFalse, 4)], null, null, 70), ExamBlueprintBuilder.Plenty(), updatedBy);

        blueprint.GetTypeCounts().Should().Equal(new ExamTypeCount(QuestionType.TrueFalse, 4));
        blueprint.QuestionCount.Should().Be(4);
        blueprint.DifficultyMix.Should().BeNull();
        blueprint.TimeLimitMinutes.Should().BeNull();
        blueprint.PassMark.Should().Be(70);
        blueprint.UpdatedBy.Should().Be(updatedBy);
        blueprint.UpdationDate.Should().BeAfter(LongAgo);
    }

    [Fact]
    public void Update_Shortfall_ThrowsAndKeepsShape()
    {
        var blueprint = _builder.BuildDefault();
        var typeCounts = blueprint.TypeCounts;

        var act = () => blueprint.Update(new ExamBlueprintShape([new ExamTypeCount(QuestionType.Mcq, 3)], null, 45, 90), new Dictionary<QuestionType, int> { [QuestionType.Mcq] = 2 }, Guid.NewGuid());

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.ExamBlueprintShortfall);
        blueprint.TypeCounts.Should().Be(typeCounts);
        blueprint.PassMark.Should().Be(50);
        blueprint.UpdatedBy.Should().Be(_builder.CreatedBy);
    }

    [Fact]
    public void Delete_UnitBlueprint_SoftDeletes()
    {
        var blueprint = _builder.BuildForUnit();
        var deletedBy = Guid.NewGuid();

        blueprint.Delete(deletedBy);

        blueprint.IsDeleted.Should().BeTrue();
        blueprint.UpdatedBy.Should().Be(deletedBy);
    }

    [Fact]
    public void Delete_SubjectDefault_ThrowsDefaultNotDeletable()
    {
        var blueprint = _builder.BuildDefault();

        var act = () => blueprint.Delete(Guid.NewGuid());

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.ExamBlueprintDefaultNotDeletable);
        blueprint.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void GetDifficultyMix_Set_RoundTrips()
    {
        var mix = new ExamDifficultyMix(30, 50, 20);

        var blueprint = ExamBlueprint.CreateForSubject(_builder.Subject, new ExamBlueprintShape([new ExamTypeCount(QuestionType.Mcq, 2)], mix, 45, 50), ExamBlueprintBuilder.Plenty(), _builder.CreatedBy);

        blueprint.DifficultyMix.Should().Be("{\"easyPercent\":30,\"mediumPercent\":50,\"hardPercent\":20}");
        blueprint.GetDifficultyMix().Should().Be(mix);
    }

    [Fact]
    public void GetTypeCounts_RoundTripsStoredJson()
    {
        var shape = ExamBlueprintBuilder.Shape(new ExamTypeCount(QuestionType.Short, 1), new ExamTypeCount(QuestionType.Multi, 3));

        var blueprint = ExamBlueprint.CreateForSubject(_builder.Subject, shape, ExamBlueprintBuilder.Plenty(), _builder.CreatedBy);

        blueprint.GetTypeCounts().Should().Equal(new ExamTypeCount(QuestionType.Multi, 3), new ExamTypeCount(QuestionType.Short, 1));
    }
}
