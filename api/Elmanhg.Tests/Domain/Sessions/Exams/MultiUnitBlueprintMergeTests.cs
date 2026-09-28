using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions.Exams;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Sessions.Exams;

public sealed class MultiUnitBlueprintMergeTests
{
    private const int MaxMinutes = MultiUnitExamBuilder.MaxTimeLimitMinutes;
    private readonly MultiUnitExamBuilder _builder = new();

    [Fact]
    public void Merge_EqualBlueprints_SplitsSizeEvenly()
    {
        var plan = MultiUnitBlueprintMerge.Merge([Part(0, 30, 50, Mcq(10)), Part(1, 30, 50, Mcq(10))], 20, MaxMinutes);

        plan.Units.Select(x => x.TypeCounts.Single()).Should().Equal(Mcq(10), Mcq(10));
    }

    [Fact]
    public void Merge_ScalesEachUnitsTypeCountsProportionally()
    {
        var plan = MultiUnitBlueprintMerge.Merge([Part(0, 30, 50, Mcq(6), Fill(4)), Part(1, 30, 50, Mcq(10))], 40, MaxMinutes);

        plan.Units[0].TypeCounts.Should().Equal(Mcq(12), Fill(8));
        plan.Units[1].TypeCounts.Should().Equal(Mcq(20));
    }

    [Fact]
    public void Merge_EqualRemainders_GoToEarlierUnitThenEarlierType()
    {
        var plan = MultiUnitBlueprintMerge.Merge([Part(0, 30, 50, Mcq(1), Fill(1)), Part(1, 30, 50, Mcq(1))], 20, MaxMinutes);

        plan.Units[0].TypeCounts.Should().Equal(Mcq(7), Fill(7));
        plan.Units[1].TypeCounts.Should().Equal(Mcq(6));
    }

    [Theory]
    [InlineData(20)]
    [InlineData(40)]
    [InlineData(60)]
    public void Merge_AnySize_TotalEqualsSize(int size)
    {
        var plan = MultiUnitBlueprintMerge.Merge([Part(0, 30, 50, Mcq(7), Fill(4)), Part(1, 30, 50, Mcq(11))], size, MaxMinutes);

        plan.QuestionCount.Should().Be(size);
    }

    [Fact]
    public void Merge_TypeCounts_SumAcrossUnitsInTypeOrder()
    {
        var plan = MultiUnitBlueprintMerge.Merge([Part(0, 30, 50, Fill(4), Mcq(6)), Part(1, 30, 50, Mcq(10))], 40, MaxMinutes);

        plan.TypeCounts.Should().Equal(Mcq(32), Fill(8));
    }

    [Fact]
    public void Merge_KeepsEachUnitsDifficultyMix()
    {
        var mix = new ExamDifficultyMix(20, 50, 30);
        var mixed = ExamBlueprint.CreateForUnit(_builder.Units[0], new ExamBlueprintShape([Mcq(10)], mix, 30, 50), ExamBlueprintBuilder.Plenty(), Guid.NewGuid());

        var plan = MultiUnitBlueprintMerge.Merge([new MultiUnitExamPart(_builder.Units[0].Id, mixed), Part(1, 30, 50, Mcq(10))], 20, MaxMinutes);

        plan.Units[0].DifficultyMix.Should().Be(mix);
        plan.Units[1].DifficultyMix.Should().BeNull();
    }

    [Fact]
    public void Merge_AllTimed_TimeLimitIsQuestionWeightedRoundedUp()
    {
        var plan = WorkedExample(MaxMinutes);
        var fractional = MultiUnitBlueprintMerge.Merge([Part(0, 25, 50, Mcq(10)), Part(1, 40, 60, Mcq(20))], 20, MaxMinutes);

        plan.TimeLimitMinutes.Should().Be(47);
        fractional.TimeLimitMinutes.Should().Be(44);
    }

    [Fact]
    public void Merge_ContributingUnitUntimed_IsUntimed()
    {
        var plan = MultiUnitBlueprintMerge.Merge([Part(0, null, 50, Mcq(10)), Part(1, 40, 60, Mcq(20))], 20, MaxMinutes);

        plan.TimeLimitMinutes.Should().BeNull();
    }

    [Fact]
    public void Merge_TimeLimitAboveMaximum_IsCapped()
    {
        var plan = WorkedExample(30);

        plan.TimeLimitMinutes.Should().Be(30);
    }

    [Fact]
    public void Merge_PassMark_QuestionWeightedRoundedHalfUp()
    {
        var plan = WorkedExample(MaxMinutes);

        plan.PassMark.Should().Be(57);
    }

    [Fact]
    public void Merge_UnitWithZeroShare_IsIgnoredForTimeAndPassMark()
    {
        var plan = MultiUnitBlueprintMerge.Merge([Part(0, null, 90, Mcq(1)), Part(1, 60, 50, Mcq(100))], 20, MaxMinutes);

        plan.Units[0].QuestionCount.Should().Be(0);
        (plan.TimeLimitMinutes, plan.PassMark).Should().Be(((int?)12, 50));
    }

    [Fact]
    public void Merge_OnePart_ThrowsInvalidOperation()
    {
        var act = () => MultiUnitBlueprintMerge.Merge([Part(0, 30, 50, Mcq(10))], 20, MaxMinutes);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Merge_SizeNotAllowed_ThrowsInvalidOperation()
    {
        var act = () => MultiUnitBlueprintMerge.Merge([Part(0, 30, 50, Mcq(10)), Part(1, 30, 50, Mcq(10))], 30, MaxMinutes);

        act.Should().Throw<InvalidOperationException>();
    }

    private static ExamTypeCount Mcq(int count) => new(QuestionType.Mcq, count);

    private static ExamTypeCount Fill(int count) => new(QuestionType.Fill, count);

    private MultiUnitExamPlan WorkedExample(int maxTimeLimitMinutes) => MultiUnitBlueprintMerge.Merge([Part(0, 30, 50, Mcq(10)), Part(1, 40, 60, Mcq(20))], 20, maxTimeLimitMinutes);

    private MultiUnitExamPart Part(int unitIndex, int? timeLimitMinutes, int passMark, params ExamTypeCount[] counts) => new(_builder.Units[unitIndex].Id, _builder.UnitBlueprint(unitIndex, timeLimitMinutes, passMark, counts));
}
