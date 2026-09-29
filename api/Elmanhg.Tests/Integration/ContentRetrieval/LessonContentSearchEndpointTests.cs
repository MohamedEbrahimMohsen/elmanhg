using Elmanhg.Domain.Lessons;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.ContentRetrieval.ContentRetrievalTestData;

namespace Elmanhg.Tests.Integration.ContentRetrieval;

[Collection(ContentRetrievalCollection.Name)]
public sealed class LessonContentSearchEndpointTests(ApiFactory factory)
{
    private const string Explanation = "<h2>قانون أوم</h2><p>شدة التيار تتناسب طرديا مع فرق الجهد عند ثبوت المقاومة</p><h2>القدرة</h2><p>القدرة الكهربية معدل بذل الشغل</p>";
    private const string Summary = "<p>ملخص سريع للدرس</p>";

    [Fact]
    public async Task PostSearch_IndexedLesson_ReturnsBestMatchFirstWithReference()
    {
        var lessonId = await SeedIndexedLessonAsync(withQuestion: false);

        var body = await SearchAsync(lessonId, new { query = "شدة التيار تتناسب طرديا مع فرق الجهد" });

        body.GetProperty("indexedAt").ValueKind.Should().Be(JsonValueKind.String);
        var first = body.GetProperty("matches")[0];
        first.GetProperty("section").GetString().Should().Be("Explanation");
        first.GetProperty("sectionTitle").GetString().Should().Be("قانون أوم");
        first.GetProperty("reference").GetString().Should().Be("explanation-1");
        first.GetProperty("score").GetDouble().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PostSearch_ServableQuestion_ReturnsQuestionChunk()
    {
        var lessonId = await SeedIndexedLessonAsync(withQuestion: true);

        var body = await SearchAsync(lessonId, new { query = "add the numbers" });

        var first = body.GetProperty("matches")[0];
        first.GetProperty("section").GetString().Should().Be("QuestionExplanation");
        first.GetProperty("questionId").GetGuid().Should().NotBeEmpty();
    }

    [Fact]
    public async Task PostSearch_RetiredQuestionAfterIndexing_IsExcluded()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var lessonId = await SeedPublishedLessonAsync(factory, Explanation, Summary, [], cancellationToken);
        var questionId = await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: true, cancellationToken);
        await ReindexAsync(factory, lessonId, cancellationToken);

        await RetireQuestionAsync(factory, questionId, cancellationToken);

        var body = await SearchAsync(lessonId, new { query = "add the numbers", top = 20 });
        Sections(body).Should().NotBeEmpty().And.NotContain("QuestionExplanation");
    }

    [Fact]
    public async Task PostSearch_IncludeQuestionExplanationsFalse_ExcludesQuestionChunks()
    {
        var lessonId = await SeedIndexedLessonAsync(withQuestion: true);

        var body = await SearchAsync(lessonId, new { query = "add the numbers", top = 20, includeQuestionExplanations = false });

        Sections(body).Should().NotBeEmpty().And.NotContain("QuestionExplanation");
    }

    [Fact]
    public async Task PostSearch_NotYetIndexed_ReturnsEmptyMatches()
    {
        var lessonId = await SeedPublishedLessonAsync(factory, Explanation, Summary, [], TestContext.Current.CancellationToken);

        var body = await SearchAsync(lessonId, new { query = "التيار" });

        body.GetProperty("indexedAt").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("matches").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task PostSearch_DraftLesson_Returns404LessonNotFound()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, cancellationToken);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Electricity", 1, cancellationToken);
        var lessonId = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Draft", 1, LessonState.Draft, cancellationToken);
        using var admin = await ClientAsync(ScopeTestData.SeedAdminAsync);

        using var response = await admin.PostAsJsonAsync(Route(lessonId), new { query = "التيار" }, cancellationToken);

        await ExpectProblemAsync(response, HttpStatusCode.NotFound, "LESSON_NOT_FOUND");
    }

    [Fact]
    public async Task PostSearch_BlankQuery_Returns422ContentSearchQueryRequired()
    {
        var lessonId = await SeedPublishedLessonAsync(factory, Explanation, Summary, [], TestContext.Current.CancellationToken);
        using var admin = await ClientAsync(ScopeTestData.SeedAdminAsync);

        using var response = await admin.PostAsJsonAsync(Route(lessonId), new { query = "  " }, TestContext.Current.CancellationToken);

        await ExpectProblemAsync(response, HttpStatusCode.UnprocessableEntity, "CONTENT_SEARCH_QUERY_REQUIRED");
    }

    [Fact]
    public async Task PostSearch_Student_Returns403()
    {
        var lessonId = await SeedIndexedLessonAsync(withQuestion: true);
        using var student = await ClientAsync(ScopeTestData.SeedStudentAsync);

        using var response = await student.PostAsJsonAsync(Route(lessonId), new { query = "add the numbers" }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostSearch_Anonymous_Returns401()
    {
        using var anonymous = factory.CreateClient();

        using var response = await anonymous.PostAsJsonAsync(Route(Guid.NewGuid()), new { query = "q" }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static string Route(Guid lessonId) => $"/api/lessons/{lessonId}/content-chunks/search";

    private async Task<Guid> SeedIndexedLessonAsync(bool withQuestion)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var lessonId = await SeedPublishedLessonAsync(factory, Explanation, Summary, ["يحسب الطالب المقاومة"], cancellationToken).ConfigureAwait(false);
        if (withQuestion)
        {
            await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: true, cancellationToken).ConfigureAwait(false);
        }

        await ReindexAsync(factory, lessonId, cancellationToken).ConfigureAwait(false);
        return lessonId;
    }

    private async Task<JsonElement> SearchAsync(Guid lessonId, object request)
    {
        using var admin = await ClientAsync(ScopeTestData.SeedAdminAsync).ConfigureAwait(false);
        using var response = await admin.PostAsJsonAsync(Route(lessonId), request, TestContext.Current.CancellationToken).ConfigureAwait(false);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpClient> ClientAsync(Func<ApiFactory, CancellationToken, Task<Elmanhg.Domain.Identity.User>> seed)
    {
        var user = await seed(factory, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return await ScopeTestData.SignedInClientAsync(factory, user, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private static async Task ExpectProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.Should().Be(status);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        problem.GetProperty("code").GetString().Should().Be(code);
    }

    private static List<string?> Sections(JsonElement body) => body.GetProperty("matches").EnumerateArray().Select(x => x.GetProperty("section").GetString()).ToList();
}
