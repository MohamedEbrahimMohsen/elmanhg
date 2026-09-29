using Elmanhg.Domain.Lessons;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Browse;

public sealed class LessonOpeningEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Post_Anonymous_Returns401()
    {
        using var anonymous = AuthTestClient.Create(factory);

        using var response = await BrowseTestData.OpenAsync(anonymous, Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_DraftLesson_Returns404()
    {
        var draft = await SeedLessonAsync(LessonState.Draft);
        var (student, client) = await SignedInStudentAsync(factory);

        using var response = await BrowseTestData.OpenAsync(client, draft);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString().Should().Be("LESSON_NOT_FOUND");
        (await BrowseTestData.ReadOpeningsAsync(factory, student.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Post_PublishedLessonTwice_StoresOneOpening()
    {
        var lessonId = await SeedLessonAsync(LessonState.Published);
        var (student, client) = await SignedInStudentAsync(factory);

        using var first = await BrowseTestData.OpenAsync(client, lessonId);
        using var second = await BrowseTestData.OpenAsync(client, lessonId);

        (first.StatusCode, second.StatusCode).Should().Be((HttpStatusCode.OK, HttpStatusCode.OK));
        (await BrowseTestData.ReadOpeningsAsync(factory, student.Id)).Should().ContainSingle().Which.LessonId.Should().Be(lessonId);
    }

    private async Task<Guid> SeedLessonAsync(LessonState state)
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, CancellationToken).ConfigureAwait(false);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, CancellationToken).ConfigureAwait(false);
        return await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Energy", 1, state, CancellationToken).ConfigureAwait(false);
    }
}
