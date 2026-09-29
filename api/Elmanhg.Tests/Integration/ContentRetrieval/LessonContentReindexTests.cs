using Elmanhg.Domain.ContentRetrieval;
using Elmanhg.Domain.Lessons;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using static Elmanhg.Tests.Integration.ContentRetrieval.ContentRetrievalTestData;

namespace Elmanhg.Tests.Integration.ContentRetrieval;

[Collection(ContentRetrievalCollection.Name)]
public sealed class LessonContentReindexTests(ApiFactory factory)
{
    private const string Explanation = "<h2>قانون أوم</h2><p>شدة التيار تتناسب طرديا مع فرق الجهد.</p>";
    private const string Summary = "<p>المقاومة تساوي الجهد على التيار.</p>";

    [Fact]
    public async Task Reindex_PublishedLessonWithServableQuestion_StoresChunksPerSection()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var lessonId = await SeedPublishedLessonAsync(factory, Explanation, Summary, ["يطبق قانون أوم"], cancellationToken);
        var questionId = await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: true, cancellationToken);

        await ReindexAsync(factory, lessonId, cancellationToken);

        var chunks = await ReadChunksAsync(factory, lessonId, cancellationToken);
        chunks.Select(x => x.Section).Should().BeEquivalentTo([LessonContentSection.Explanation, LessonContentSection.Objectives, LessonContentSection.Summary, LessonContentSection.QuestionExplanation]);
        var questionChunk = chunks.Single(x => x.Section == LessonContentSection.QuestionExplanation);
        questionChunk.QuestionId.Should().Be(questionId);
        questionChunk.QuestionVersion.Should().Be(1);
        chunks.Single(x => x.Section == LessonContentSection.Explanation).SectionTitle.Should().Be("قانون أوم");
        var index = await ReadIndexAsync(factory, lessonId, cancellationToken);
        index!.SourceUpdatedAt.Should().Be((await ContentTestData.ReadLessonAsync(factory, lessonId, cancellationToken)).UpdationDate);
        index.ChunkCount.Should().Be(chunks.Count);
        index.EmbeddingModel.Should().Be("fake");
    }

    [Fact]
    public async Task Reindex_Twice_ReplacesChunksWithoutDuplicates()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var lessonId = await SeedPublishedLessonAsync(factory, Explanation, Summary, [], cancellationToken);
        await ReindexAsync(factory, lessonId, cancellationToken);
        var first = await ReadChunksAsync(factory, lessonId, cancellationToken);

        await ReindexAsync(factory, lessonId, cancellationToken);

        var second = await ReadChunksAsync(factory, lessonId, cancellationToken);
        second.Should().HaveCount(first.Count);
        second.Select(x => x.Id).Should().NotIntersectWith(first.Select(x => x.Id));
    }

    [Fact]
    public async Task Reindex_PendingQuestion_IsNotIndexed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var lessonId = await SeedPublishedLessonAsync(factory, Explanation, Summary, [], cancellationToken);
        await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: false, cancellationToken);

        await ReindexAsync(factory, lessonId, cancellationToken);

        (await ReadChunksAsync(factory, lessonId, cancellationToken)).Should().NotBeEmpty().And.NotContain(x => x.Section == LessonContentSection.QuestionExplanation);
    }

    [Fact]
    public async Task Reindex_AfterUnpublish_RemovesChunksAndIndex()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var lessonId = await SeedPublishedLessonAsync(factory, Explanation, Summary, [], cancellationToken);
        await ReindexAsync(factory, lessonId, cancellationToken);
        var admin = await ScopeTestData.SeedAdminAsync(factory, cancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, admin, cancellationToken);
        using var response = await client.PostAsync($"/api/lessons/{lessonId}/unpublish", null, cancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadStaleIdsAsync(factory, [], cancellationToken)).Should().Contain(lessonId);

        await ReindexAsync(factory, lessonId, cancellationToken);

        (await ReadChunksAsync(factory, lessonId, cancellationToken)).Should().BeEmpty();
        (await ReadIndexAsync(factory, lessonId, cancellationToken)).Should().BeNull();
    }

    [Fact]
    public async Task StaleIds_PublishedLessonWithoutIndex_IsListed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var lessonId = await SeedPublishedLessonAsync(factory, Explanation, Summary, [], cancellationToken);

        var stale = await ReadStaleIdsAsync(factory, [], cancellationToken);

        stale.Should().Contain(lessonId);
    }

    [Fact]
    public async Task StaleIds_FreshlyIndexedLesson_IsNotListed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var withQuestion = await SeedPublishedLessonAsync(factory, Explanation, Summary, [], cancellationToken);
        await QuestionTestData.SeedQuestionAsync(factory, withQuestion, approved: true, cancellationToken);
        var withoutQuestions = await SeedPublishedLessonAsync(factory, Explanation, Summary, [], cancellationToken);
        await ReindexAsync(factory, withQuestion, cancellationToken);
        await ReindexAsync(factory, withoutQuestions, cancellationToken);

        var stale = await ReadStaleIdsAsync(factory, [], cancellationToken);

        stale.Should().NotContain([withQuestion, withoutQuestions]);
    }

    [Fact]
    public async Task StaleIds_LessonTouchedAfterIndexing_IsListed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var lessonId = await SeedPublishedLessonAsync(factory, Explanation, Summary, [], cancellationToken);
        await ReindexAsync(factory, lessonId, cancellationToken);

        await TouchLessonAsync(factory, lessonId, cancellationToken);

        (await ReadStaleIdsAsync(factory, [], cancellationToken)).Should().Contain(lessonId);
    }

    [Fact]
    public async Task StaleIds_QuestionApprovedAfterIndexing_IsListed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var lessonId = await SeedPublishedLessonAsync(factory, Explanation, Summary, [], cancellationToken);
        await ReindexAsync(factory, lessonId, cancellationToken);

        await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: true, cancellationToken);

        (await ReadStaleIdsAsync(factory, [], cancellationToken)).Should().Contain(lessonId);
    }

    [Fact]
    public async Task StaleIds_DraftLessonAndExcludedId_AreNotListed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var excludedId = await SeedPublishedLessonAsync(factory, Explanation, Summary, [], cancellationToken);
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, cancellationToken);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Electricity", 1, cancellationToken);
        var draftId = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Draft", 1, LessonState.Draft, cancellationToken);

        var stale = await ReadStaleIdsAsync(factory, [excludedId], cancellationToken);

        stale.Should().NotContain([excludedId, draftId]);
    }
}
