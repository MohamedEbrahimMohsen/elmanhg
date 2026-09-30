using Elmanhg.Domain.Lessons;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using static Elmanhg.Tests.Integration.Dashboard.DashboardTestData;

namespace Elmanhg.Tests.Integration.Dashboard;

public sealed class ContentMetricsEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_SubjectFilter_CountsLessonsQuestionsAndServable()
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Chemistry", 1, CancellationToken);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Atoms", 1, CancellationToken);
        var publishedLessonId = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Electrons", 1, LessonState.Published, CancellationToken);
        await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Protons", 2, LessonState.Draft, CancellationToken);
        await QuestionTestData.SeedQuestionAsync(factory, publishedLessonId, approved: true, CancellationToken);
        await QuestionTestData.SeedQuestionAsync(factory, publishedLessonId, approved: false, CancellationToken);
        await QuestionTestData.SeedRetiredQuestionAsync(factory, publishedLessonId, CancellationToken);
        using var client = await AdminClientAsync(factory);

        var body = await GetJsonAsync(client, $"content?subjectId={subjectId}");

        body.GetProperty("subjectId").GetGuid().Should().Be(subjectId);
        body.GetProperty("subjects").GetInt32().Should().Be(1);
        body.GetProperty("units").GetInt32().Should().Be(1);
        body.GetProperty("lessonsPublished").GetInt32().Should().Be(1);
        body.GetProperty("lessonsDraft").GetInt32().Should().Be(1);
        body.GetProperty("lessonsArchived").GetInt32().Should().Be(0);
        body.GetProperty("questionsApproved").GetInt32().Should().Be(1);
        body.GetProperty("questionsPending").GetInt32().Should().Be(1);
        body.GetProperty("questionsRetired").GetInt32().Should().Be(1);
        body.GetProperty("questionsRejected").GetInt32().Should().Be(0);
        body.GetProperty("servableTotal").GetInt32().Should().Be(1);
    }
}
