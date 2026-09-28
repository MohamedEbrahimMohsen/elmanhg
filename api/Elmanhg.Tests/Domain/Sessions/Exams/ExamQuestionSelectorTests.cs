using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions.Exams;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Sessions.Exams;

public sealed class ExamQuestionSelectorTests
{
    private const int Seed = 7;
    private static readonly HashSet<Guid> NoneMastered = [];
    private readonly Guid _lessonId = Guid.NewGuid();

    [Fact]
    public void Select_TypeCounts_PicksExactCountPerType()
    {
        var candidates = Pool(QuestionType.Mcq, QuestionDifficulty.Medium, 5).Concat(Pool(QuestionType.Fill, QuestionDifficulty.Medium, 3)).ToList();

        var result = ExamQuestionSelector.Select(candidates, NoneMastered, [new ExamTypeCount(QuestionType.Mcq, 2), new ExamTypeCount(QuestionType.Fill, 1)], null, new Random(Seed));

        TypesOf(result, candidates).Should().Equal(QuestionType.Mcq, QuestionType.Mcq, QuestionType.Fill);
    }

    [Fact]
    public void Select_NotMasteredAvailable_PrefersNotMastered()
    {
        var candidates = Pool(QuestionType.Mcq, QuestionDifficulty.Medium, 6);
        var mastered = candidates.Take(3).Select(x => x.QuestionId).ToHashSet();

        var result = ExamQuestionSelector.Select(candidates, mastered, [new ExamTypeCount(QuestionType.Mcq, 3)], null, new Random(Seed));

        result.Should().HaveCount(3).And.NotIntersectWith(mastered);
    }

    [Fact]
    public void Select_NotEnoughNotMastered_FillsWithMastered()
    {
        var candidates = Pool(QuestionType.Mcq, QuestionDifficulty.Medium, 4);
        var mastered = candidates.Take(3).Select(x => x.QuestionId).ToHashSet();

        var result = ExamQuestionSelector.Select(candidates, mastered, [new ExamTypeCount(QuestionType.Mcq, 3)], null, new Random(Seed));

        result.Should().HaveCount(3).And.Contain(candidates[3].QuestionId);
        result.Count(mastered.Contains).Should().Be(2);
    }

    [Fact]
    public void Select_DifficultyMix_MeetsTargetsWhenPoolAllows()
    {
        var candidates = Pool(QuestionType.Mcq, QuestionDifficulty.Easy, 10).Concat(Pool(QuestionType.Mcq, QuestionDifficulty.Medium, 10)).Concat(Pool(QuestionType.Mcq, QuestionDifficulty.Hard, 10)).ToList();

        var result = ExamQuestionSelector.Select(candidates, NoneMastered, [new ExamTypeCount(QuestionType.Mcq, 10)], new ExamDifficultyMix(30, 50, 20), new Random(Seed));

        var difficulties = result.Select(id => candidates.Single(x => x.QuestionId == id).Difficulty).ToList();
        difficulties.Count(x => x == QuestionDifficulty.Easy).Should().Be(3);
        difficulties.Count(x => x == QuestionDifficulty.Medium).Should().Be(5);
        difficulties.Count(x => x == QuestionDifficulty.Hard).Should().Be(2);
    }

    [Fact]
    public void Select_DifficultyShort_FillsFromOtherDifficulties()
    {
        var candidates = Pool(QuestionType.Mcq, QuestionDifficulty.Easy, 6).Concat(Pool(QuestionType.Mcq, QuestionDifficulty.Medium, 6)).ToList();

        var result = ExamQuestionSelector.Select(candidates, NoneMastered, [new ExamTypeCount(QuestionType.Mcq, 10)], new ExamDifficultyMix(30, 50, 20), new Random(Seed));

        result.Should().HaveCount(10).And.OnlyHaveUniqueItems();
    }

    [Fact]
    public void Select_OrdersByTypeThenDifficulty()
    {
        var hardMcq = Candidate(QuestionType.Mcq, QuestionDifficulty.Hard);
        var easyMcq = Candidate(QuestionType.Mcq, QuestionDifficulty.Easy);
        var easyFill = Candidate(QuestionType.Fill, QuestionDifficulty.Easy);

        var result = ExamQuestionSelector.Select([easyFill, hardMcq, easyMcq], NoneMastered, [new ExamTypeCount(QuestionType.Fill, 1), new ExamTypeCount(QuestionType.Mcq, 2)], null, new Random(Seed));

        result.Should().Equal(easyMcq.QuestionId, hardMcq.QuestionId, easyFill.QuestionId);
    }

    [Fact]
    public void Select_SameSeed_ReturnsSameOrder()
    {
        var candidates = Pool(QuestionType.Mcq, QuestionDifficulty.Medium, 8);
        var mastered = candidates.Take(2).Select(x => x.QuestionId).ToHashSet();
        List<ExamTypeCount> counts = [new ExamTypeCount(QuestionType.Mcq, 5)];

        var first = ExamQuestionSelector.Select(candidates, mastered, counts, null, new Random(Seed));
        var second = ExamQuestionSelector.Select(candidates, mastered, counts, null, new Random(Seed));
        var permuted = ExamQuestionSelector.Select(Enumerable.Reverse(candidates).ToList(), mastered, counts, null, new Random(Seed));

        second.Should().Equal(first);
        permuted.Should().Equal(first);
    }

    [Fact]
    public void Select_PoolSmallerThanCount_ReturnsAvailable()
    {
        var candidates = Pool(QuestionType.Mcq, QuestionDifficulty.Medium, 2);

        var result = ExamQuestionSelector.Select(candidates, NoneMastered, [new ExamTypeCount(QuestionType.Mcq, 5)], new ExamDifficultyMix(30, 50, 20), new Random(Seed));

        result.Should().BeEquivalentTo(candidates.Select(x => x.QuestionId));
    }

    [Fact]
    public void Select_DuplicateCandidates_NeverRepeats()
    {
        var candidates = Pool(QuestionType.Mcq, QuestionDifficulty.Medium, 3);

        var result = ExamQuestionSelector.Select([.. candidates, .. candidates], NoneMastered, [new ExamTypeCount(QuestionType.Mcq, 5)], null, new Random(Seed));

        result.Should().HaveCount(3).And.OnlyHaveUniqueItems();
    }

    private List<ExamCandidate> Pool(QuestionType type, QuestionDifficulty difficulty, int count)
    {
        return Enumerable.Range(0, count)
            .Select(_ => Candidate(type, difficulty))
            .ToList();
    }

    private ExamCandidate Candidate(QuestionType type, QuestionDifficulty difficulty) => new(Guid.NewGuid(), _lessonId, type, difficulty);

    private static List<QuestionType> TypesOf(IEnumerable<Guid> ids, IReadOnlyCollection<ExamCandidate> candidates) => ids.Select(id => candidates.Single(x => x.QuestionId == id).Type).ToList();
}
