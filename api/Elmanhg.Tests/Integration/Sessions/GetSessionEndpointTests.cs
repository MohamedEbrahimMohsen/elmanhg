using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Sessions;

public sealed class GetSessionEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Get_AfterAnsweringFirst_ReturnsSavedProgress()
    {
        var (client, sessionId) = await StartAsync();
        var first = (await ReadSessionAsync(factory, sessionId)).Items.Single(x => x.Position == 1);
        using var answer = await AnswerAsync(client, sessionId, first.QuestionId, "b");

        using var response = await client.GetAsync($"{Route}/{sessionId}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("currentPosition").GetInt32().Should().Be(2);
        var items = body.GetProperty("items");
        items[0].GetProperty("attempt").ValueKind.Should().Be(JsonValueKind.Object);
        items[1].GetProperty("attempt").ValueKind.Should().Be(JsonValueKind.Null);
        items[1].GetProperty("correctAnswer").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Get_OtherStudentsSession_Returns404()
    {
        var (_, sessionId) = await StartAsync();
        var (_, other) = await SignedInStudentAsync(factory);

        using var response = await other.GetAsync($"{Route}/{sessionId}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("code").GetString().Should().Be("SESSION_NOT_FOUND");
    }

    [Fact]
    public async Task Get_Teacher_Returns403()
    {
        var (_, sessionId) = await StartAsync();
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, TestContext.Current.CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, TestContext.Current.CancellationToken);

        using var response = await client.GetAsync($"{Route}/{sessionId}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        var (_, sessionId) = await StartAsync();
        using var anonymous = AuthTestClient.Create(factory);

        using var response = await anonymous.GetAsync($"{Route}/{sessionId}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<(HttpClient Client, Guid SessionId)> StartAsync()
    {
        var (lessonId, _) = await SeedServableLessonAsync(factory, 2).ConfigureAwait(false);
        var (_, client) = await SignedInStudentAsync(factory).ConfigureAwait(false);
        var body = await StartQuizAsync(client, lessonId).ConfigureAwait(false);
        return (client, body.GetProperty("id").GetGuid());
    }
}
