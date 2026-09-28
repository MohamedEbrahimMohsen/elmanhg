using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Exams.ExamTestData;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Exams;

public sealed class GetExamSessionEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Get_OpenExam_ReturnsSavedAnswersWithoutKeys()
    {
        var (_, unitId, _, _) = await SeedExamUnitAsync(factory, 2);
        var (_, client) = await SignedInStudentAsync(factory);
        var started = await StartAsync(client, unitId);
        var sessionId = started.GetProperty("id").GetGuid();
        var questionId = ItemQuestionIds(started)[0];
        using var save = await SaveAsync(client, sessionId, questionId, "b");

        using var response = await client.GetAsync($"{ExamTestData.Route}/{sessionId}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var item = body.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("questionId").GetGuid() == questionId);
        item.GetProperty("savedAnswer").GetProperty("optionId").GetString().Should().Be("b");
        item.GetProperty("correctAnswer").ValueKind.Should().Be(JsonValueKind.Null);
        item.GetProperty("attempt").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Get_OtherStudentExam_Returns404()
    {
        var (_, unitId, _, _) = await SeedExamUnitAsync(factory, 1);
        var (_, owner) = await SignedInStudentAsync(factory);
        var started = await StartAsync(owner, unitId);
        var (_, other) = await SignedInStudentAsync(factory);

        using var response = await other.GetAsync($"{ExamTestData.Route}/{started.GetProperty("id").GetGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("SESSION_NOT_FOUND");
    }

    [Fact]
    public async Task Get_QuizSession_Returns404()
    {
        var (lessonId, _) = await SeedServableLessonAsync(factory, 1);
        var (_, client) = await SignedInStudentAsync(factory);
        var quiz = await StartQuizAsync(client, lessonId);

        using var response = await client.GetAsync($"{ExamTestData.Route}/{quiz.GetProperty("id").GetGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("SESSION_NOT_FOUND");
    }
}
