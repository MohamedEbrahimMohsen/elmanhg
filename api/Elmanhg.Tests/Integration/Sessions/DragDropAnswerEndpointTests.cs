using Elmanhg.Domain.Questions.Schemas;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Builders.QuestionBuilder;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Sessions;

public sealed class DragDropAnswerEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Start_LessonWithDragDrop_ServesDiagramWithResolvedImageUrl()
    {
        var (lessonId, _) = await SeedServableLessonAsync(factory, 4);
        await QuestionTestData.SeedDragDropQuestionAsync(factory, lessonId, TestContext.Current.CancellationToken);
        var (_, client) = await SignedInStudentAsync(factory);

        var session = await StartQuizAsync(client, lessonId, 5);

        var item = session.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("type").GetString() == "DragDrop");
        item.GetProperty("body").GetProperty("image").GetProperty("url").GetString().Should().Be($"/api/media/{ForLesson(DragDropImageKey, lessonId)}");
        item.GetProperty("correctAnswer").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Post_EveryItemInPlace_RecordsCorrectAttemptAndRevealsKey()
    {
        var (client, sessionId, questionId) = await StartDragDropQuizAsync();

        using var response = await PostAsync(client, sessionId, questionId, Placements(("z1", ["i1", "i2"]), ("z2", ["i4", "i3"])));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        item.GetProperty("attempt").GetProperty("outcome").GetString().Should().Be("Correct");
        item.GetProperty("correctAnswer").GetProperty("zones").GetArrayLength().Should().Be(2);
        var attempt = (await ReadAttemptsAsync(factory, sessionId)).Should().ContainSingle().Subject;
        attempt.Score.Should().Be(4m);
        QuestionJson.AreEquivalent(attempt.Answer, """{"placements":[{"zoneId":"z1","itemIds":["i1","i2"]},{"zoneId":"z2","itemIds":["i4","i3"]}]}""").Should().BeTrue(attempt.Answer);
    }

    [Fact]
    public async Task Post_OrderedZoneSwapped_RecordsPartialWithTally()
    {
        var (client, sessionId, questionId) = await StartDragDropQuizAsync();
        client.DefaultRequestHeaders.Add("Accept-Language", "ar");

        using var response = await PostAsync(client, sessionId, questionId, Placements(("z1", ["i2", "i1"]), ("z2", ["i3", "i4"])));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var attempt = (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("attempt");
        attempt.GetProperty("outcome").GetString().Should().Be("Partial");
        attempt.GetProperty("score").GetDecimal().Should().Be(2m);
        attempt.GetProperty("feedback").GetString().Should().Be("العناصر في أماكنها الصحيحة: 2 من 4، والعناصر المشتِّتة الموضوعة: 0.");
    }

    [Fact]
    public async Task Post_TooManyPlacements_Returns422AttemptAnswerTooLong()
    {
        var (client, sessionId, questionId) = await StartDragDropQuizAsync();
        var placements = Enumerable.Range(0, 21)
            .Select(x => new { zoneId = $"z{x}", itemIds = new[] { "i1" } })
            .ToArray();

        using var response = await PostAsync(client, sessionId, questionId, new { placements });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Be("ATTEMPT_ANSWER_TOO_LONG");
        (await ReadAttemptsAsync(factory, sessionId)).Should().BeEmpty();
    }

    [Fact]
    public async Task Post_NullItemId_Returns422QuestionAnswerInvalid()
    {
        var (client, sessionId, questionId) = await StartDragDropQuizAsync();

        using var response = await PostAsync(client, sessionId, questionId, new { placements = new[] { new { zoneId = "z1", itemIds = new string?[] { null } } } });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Be("QUESTION_ANSWER_INVALID");
        (await ReadAttemptsAsync(factory, sessionId)).Should().BeEmpty();
    }

    private async Task<(HttpClient Client, Guid SessionId, Guid QuestionId)> StartDragDropQuizAsync()
    {
        var (lessonId, _) = await SeedServableLessonAsync(factory, 4);
        var dragDropId = await QuestionTestData.SeedDragDropQuestionAsync(factory, lessonId, TestContext.Current.CancellationToken);
        var (_, client) = await SignedInStudentAsync(factory);
        var session = await StartQuizAsync(client, lessonId, 5);
        session.GetProperty("items").EnumerateArray().Should().Contain(x => x.GetProperty("questionId").GetGuid() == dragDropId);
        return (client, session.GetProperty("id").GetGuid(), dragDropId);
    }

    private static object Placements(params (string ZoneId, string[] ItemIds)[] placements) => new { placements = placements.Select(x => new { zoneId = x.ZoneId, itemIds = x.ItemIds }).ToArray() };

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, Guid sessionId, Guid questionId, object answer)
    {
        return client.PostAsJsonAsync($"{Route}/{sessionId}/answers", new { questionId, answer, timeTakenMilliseconds = 1000 }, TestContext.Current.CancellationToken);
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return body.GetProperty("code").GetString();
    }
}
