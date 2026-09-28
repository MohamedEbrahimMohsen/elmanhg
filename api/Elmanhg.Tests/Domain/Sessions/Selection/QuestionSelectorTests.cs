using Elmanhg.Domain.Sessions.Selection;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Sessions.Selection;

public sealed class QuestionSelectorTests
{
    private const int Seed = 42;
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Select_AllBuckets_OrdersUnseenThenLastWrongThenCorrectOnceThenRest()
    {
        var unseen = Ids(2);
        var wrong = Ids(2);
        var correctOnce = Ids(2);
        var rest = Ids(2);
        List<QuestionAttemptSummary> summaries = [Wrong(wrong[0], T0), Wrong(wrong[1], T0.AddMinutes(1)), CorrectOnce(correctOnce[0], T0), CorrectOnce(correctOnce[1], T0.AddMinutes(1)), Rest(rest[0], T0), Rest(rest[1], T0.AddMinutes(1))];

        var result = QuestionSelector.Select([.. rest, .. correctOnce, .. wrong, .. unseen], summaries, 8, new Random(Seed));

        result.Should().HaveCount(8);
        result[0..2].Should().BeEquivalentTo(unseen);
        result[2..4].Should().BeEquivalentTo(wrong);
        result[4..6].Should().BeEquivalentTo(correctOnce);
        result[6..8].Should().BeEquivalentTo(rest);
    }

    [Fact]
    public void Select_UnseenFillCount_ExcludesSeenQuestions()
    {
        var unseen = Ids(3);
        var wrong = Ids(3);

        var result = QuestionSelector.Select([.. wrong, .. unseen], wrong.Select(x => Wrong(x, T0)).ToList(), 3, new Random(Seed));

        result.Should().BeEquivalentTo(unseen);
    }

    [Fact]
    public void Select_PoolSmallerThanCount_ReturnsWholePool()
    {
        var candidates = Ids(3);

        var result = QuestionSelector.Select(candidates, [Wrong(candidates[1], T0), Rest(candidates[2], T0)], 10, new Random(Seed));

        result.Should().HaveCount(3).And.OnlyHaveUniqueItems().And.BeEquivalentTo(candidates);
    }

    [Fact]
    public void Select_PoolLargerThanCount_ReturnsCountUniqueCandidates()
    {
        var candidates = Ids(12);

        var result = QuestionSelector.Select(candidates, [], 5, new Random(Seed));

        result.Should().HaveCount(5).And.OnlyHaveUniqueItems().And.BeSubsetOf(candidates);
    }

    [Fact]
    public void Select_DuplicateCandidateIds_ReturnsEachOnce()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var result = QuestionSelector.Select([a, a, b], [], 5, new Random(Seed));

        result.Should().HaveCount(2).And.BeEquivalentTo([a, b]);
    }

    [Fact]
    public void Select_LastWrong_OrdersOldestFirst()
    {
        var ids = Ids(3);
        List<QuestionAttemptSummary> summaries = [Wrong(ids[0], T0.AddMinutes(2)), Wrong(ids[1], T0), Wrong(ids[2], T0.AddMinutes(1))];

        var result = QuestionSelector.Select(ids, summaries, 3, new Random(Seed));

        result.Should().Equal(ids[1], ids[2], ids[0]);
    }

    [Fact]
    public void Select_CorrectOnceBeforeRest_EvenWhenRestIsOlder()
    {
        var correctOnce = Guid.NewGuid();
        var rest = Guid.NewGuid();

        var result = QuestionSelector.Select([rest, correctOnce], [CorrectOnce(correctOnce, T0.AddMinutes(10)), Rest(rest, T0)], 2, new Random(Seed));

        result.Should().Equal(correctOnce, rest);
    }

    [Fact]
    public void Select_SameSeed_ReturnsSameSequence()
    {
        var (candidates, summaries) = Mixed();

        var first = QuestionSelector.Select(candidates, summaries, 10, new Random(Seed));
        var second = QuestionSelector.Select(candidates, summaries, 10, new Random(Seed));

        first.Should().Equal(second);
    }

    [Fact]
    public void Select_CandidateInputOrderReversed_ReturnsSameSequence()
    {
        var (candidates, summaries) = Mixed();
        var reversed = candidates.AsEnumerable().Reverse().ToList();

        var forward = QuestionSelector.Select(candidates, summaries, 10, new Random(Seed));
        var backward = QuestionSelector.Select(reversed, summaries, 10, new Random(Seed));

        backward.Should().Equal(forward);
    }

    [Fact]
    public void Select_UnseenBucket_IsShuffledAcrossSeeds()
    {
        var candidates = Ids(5);

        var results = Enumerable.Range(1, 20).Select(seed => QuestionSelector.Select(candidates, [], 5, new Random(seed))).ToList();

        results.Select(x => x[0]).Distinct().Should().HaveCountGreaterThan(1);
        results.Should().AllSatisfy(x => x.Should().BeEquivalentTo(candidates));
    }

    [Fact]
    public void Select_CorrectOnceBucket_IsShuffledAcrossSeeds()
    {
        var candidates = Ids(5);
        var summaries = candidates.Select((x, index) => CorrectOnce(x, T0.AddMinutes(index))).ToList();

        var firsts = Enumerable.Range(1, 20).Select(seed => QuestionSelector.Select(candidates, summaries, 5, new Random(seed))[0]).ToList();

        firsts.Distinct().Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void Select_RestBucket_FavoursLeastRecentlySeen()
    {
        var candidates = Ids(5);
        var summaries = candidates.Select((x, index) => Rest(x, T0.AddMinutes(index))).ToList();

        var picks = Enumerable.Range(0, 1000).Select(seed => QuestionSelector.Select(candidates, summaries, 1, new Random(seed))[0]).ToList();

        var counts = candidates.ToDictionary(x => x, x => picks.Count(pick => pick == x));
        counts[candidates[0]].Should().BeGreaterThan(2 * counts[candidates[4]]);
        counts.Values.Should().AllSatisfy(x => x.Should().BePositive());
    }

    [Fact]
    public void Select_SummaryForNonCandidate_IsIgnored()
    {
        var a = Guid.NewGuid();

        var result = QuestionSelector.Select([a], [Wrong(Guid.NewGuid(), T0)], 5, new Random(Seed));

        result.Should().Equal(a);
    }

    [Fact]
    public void Select_CountZero_ThrowsArgumentOutOfRange()
    {
        var act = () => QuestionSelector.Select([Guid.NewGuid()], [], 0, new Random(Seed));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static List<Guid> Ids(int count) => Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();

    private static (List<Guid> Candidates, List<QuestionAttemptSummary> Summaries) Mixed()
    {
        var candidates = Ids(10);
        List<QuestionAttemptSummary> summaries = [Wrong(candidates[2], T0), Wrong(candidates[3], T0), CorrectOnce(candidates[4], T0), CorrectOnce(candidates[5], T0.AddMinutes(1)), Rest(candidates[6], T0), Rest(candidates[7], T0.AddMinutes(1)), Rest(candidates[8], T0.AddMinutes(2)), Rest(candidates[9], T0.AddMinutes(3))];
        return (candidates, summaries);
    }

    private static QuestionAttemptSummary Wrong(Guid id, DateTimeOffset at) => new(id, 1, 0, at, null);

    private static QuestionAttemptSummary CorrectOnce(Guid id, DateTimeOffset at) => new(id, 1, 1, at, at);

    private static QuestionAttemptSummary Rest(Guid id, DateTimeOffset at) => new(id, 2, 2, at, at);
}
