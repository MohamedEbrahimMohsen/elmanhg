using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions.Exams;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Sessions.Exams;

public sealed class MultiUnitExamQuestionSelectorTests
{
    private const int Seed = 7;
    private static readonly HashSet<Guid> NoneMastered = [];
    private readonly Guid _unitA = Guid.NewGuid();
    private readonly Guid _unitB = Guid.NewGuid();

    [Fact]
    public void Select_EachUnitHasEnough_TakesPlannedCountFromEachUnit()
    {
        var pools = Pools(Pool(QuestionType.Mcq, QuestionDifficulty.Medium, 15), Pool(QuestionType.Mcq, QuestionDifficulty.Medium, 15));

        var result = MultiUnitExamQuestionSelector.Select(Plan(Mcq(7), Mcq(13)), pools, NoneMastered, new Random(Seed));

        (result.Count(id => InPool(pools, _unitA, id)), result.Count(id => InPool(pools, _unitB, id))).Should().Be((7, 13));
    }

    [Fact]
    public void Select_UnitShortOfType_FillsFromOtherSelectedUnits()
    {
        var pools = Pools(Pool(QuestionType.Mcq, QuestionDifficulty.Medium, 4), Pool(QuestionType.Mcq, QuestionDifficulty.Medium, 20));

        var result = MultiUnitExamQuestionSelector.Select(Plan(Mcq(10), Mcq(10)), pools, NoneMastered, new Random(Seed));

        result.Should().HaveCount(20).And.OnlyHaveUniqueItems();
        (result.Count(id => InPool(pools, _unitA, id)), result.Count(id => InPool(pools, _unitB, id))).Should().Be((4, 16));
    }

    [Fact]
    public void Select_PrefersNotMasteredAcrossUnits()
    {
        var pools = Pools(Pool(QuestionType.Mcq, QuestionDifficulty.Medium, 6), Pool(QuestionType.Mcq, QuestionDifficulty.Medium, 6));
        var mastered = pools.Values
            .SelectMany(x => x.Take(3))
            .Select(x => x.QuestionId)
            .ToHashSet();

        var result = MultiUnitExamQuestionSelector.Select(Plan(Mcq(3), Mcq(3)), pools, mastered, new Random(Seed));

        result.Should().HaveCount(6).And.NotIntersectWith(mastered);
    }

    [Fact]
    public void Select_OrdersByTypeThenDifficulty()
    {
        var poolA = Pool(QuestionType.Fill, QuestionDifficulty.Easy, 3).Concat(Pool(QuestionType.Mcq, QuestionDifficulty.Hard, 3)).ToList();
        var poolB = Pool(QuestionType.Mcq, QuestionDifficulty.Easy, 3).Concat(Pool(QuestionType.Fill, QuestionDifficulty.Hard, 3)).ToList();
        var pools = Pools(poolA, poolB);
        var all = poolA.Concat(poolB).ToDictionary(x => x.QuestionId);

        var result = MultiUnitExamQuestionSelector.Select(Plan([Mcq(2), Fill(2)], [Mcq(2), Fill(2)]), pools, NoneMastered, new Random(Seed));

        var keys = result.Select(id => ((int)all[id].Type * 10) + (int)all[id].Difficulty).ToList();
        keys.Should().HaveCount(8).And.BeInAscendingOrder();
    }

    [Fact]
    public void Select_AppliesEachUnitsDifficultyMix()
    {
        var poolA = Pool(QuestionType.Mcq, QuestionDifficulty.Easy, 10).Concat(Pool(QuestionType.Mcq, QuestionDifficulty.Hard, 10)).ToList();
        var pools = Pools(poolA, Pool(QuestionType.Mcq, QuestionDifficulty.Easy, 10));
        var plan = new MultiUnitExamPlan([new MultiUnitExamUnitPlan(_unitA, false, [Mcq(5)], new ExamDifficultyMix(0, 0, 100)), new MultiUnitExamUnitPlan(_unitB, false, [Mcq(5)], null)], 30, 50);

        var result = MultiUnitExamQuestionSelector.Select(plan, pools, NoneMastered, new Random(Seed));

        result.Where(id => InPool(pools, _unitA, id)).Should().HaveCount(5).And.AllSatisfy(id => poolA.Single(x => x.QuestionId == id).Difficulty.Should().Be(QuestionDifficulty.Hard));
    }

    [Fact]
    public void Select_SameSeed_ReturnsSameSelection()
    {
        var pools = Pools(Pool(QuestionType.Mcq, QuestionDifficulty.Medium, 12), Pool(QuestionType.Mcq, QuestionDifficulty.Medium, 12));

        var first = MultiUnitExamQuestionSelector.Select(Plan(Mcq(10), Mcq(10)), pools, NoneMastered, new Random(Seed));
        var second = MultiUnitExamQuestionSelector.Select(Plan(Mcq(10), Mcq(10)), pools, NoneMastered, new Random(Seed));

        first.Should().Equal(second);
    }

    private static ExamTypeCount Mcq(int count) => new(QuestionType.Mcq, count);

    private static ExamTypeCount Fill(int count) => new(QuestionType.Fill, count);

    private static bool InPool(Dictionary<Guid, List<ExamCandidate>> pools, Guid unitId, Guid questionId) => pools[unitId].Any(x => x.QuestionId == questionId);

    private static List<ExamCandidate> Pool(QuestionType type, QuestionDifficulty difficulty, int count) => Enumerable.Range(0, count).Select(_ => new ExamCandidate(Guid.NewGuid(), Guid.NewGuid(), type, difficulty)).ToList();

    private MultiUnitExamPlan Plan(ExamTypeCount a, ExamTypeCount b) => Plan([a], [b]);

    private MultiUnitExamPlan Plan(List<ExamTypeCount> a, List<ExamTypeCount> b) => new([new MultiUnitExamUnitPlan(_unitA, false, a, null), new MultiUnitExamUnitPlan(_unitB, false, b, null)], 30, 50);

    private Dictionary<Guid, List<ExamCandidate>> Pools(List<ExamCandidate> a, List<ExamCandidate> b) => new() { [_unitA] = a, [_unitB] = b };
}
