using Elmanhg.Domain.EssayGrading;
using Elmanhg.Tests.Integration.EssayGrading;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Exams.ExamTestData;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Exams;

public sealed class ExamEssayEndpointTests(ApiFactory factory)
{
    private const string EssayText = "القصور الذاتي هو ممانعة الجسم لتغيير حالته الحركية.";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SaveAnswer_EssayOf10000Characters_Returns200()
    {
        var (client, sessionId, _, essayId) = await StartEssayExamAsync();

        using var response = await SaveEssayAsync(client, sessionId, essayId, new string('ب', 10000));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Submit_WrittenEssay_CreatesPendingGradeAndScoresOthers()
    {
        var (client, sessionId, mcqId, essayId) = await StartEssayExamAsync();
        using var mcq = await SaveAsync(client, sessionId, mcqId, "b");
        using var essay = await SaveEssayAsync(client, sessionId, essayId, EssayText);

        using var response = await SubmitAsync(client, sessionId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        (body.GetProperty("scorePercent").GetDecimal(), body.GetProperty("isPassed").GetBoolean()).Should().Be((16.67m, false));
        var item = body.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("questionId").GetGuid() == essayId);
        item.GetProperty("attempt").ValueKind.Should().Be(JsonValueKind.Null);
        item.GetProperty("savedAnswer").GetProperty("text").GetString().Should().Be(EssayText);
        (await EssayGradingTestData.ReadGradeForAsync(factory, sessionId, essayId)).Status.Should().Be(EssayGradeStatus.Pending);
    }

    [Fact]
    public async Task GradeEssay_AfterSubmit_RaisesScorePercent()
    {
        var (client, sessionId, mcqId, essayId) = await StartEssayExamAsync();
        using var mcq = await SaveAsync(client, sessionId, mcqId, "b");
        using var essay = await SaveEssayAsync(client, sessionId, essayId, EssayText);
        using var submitted = await SubmitAsync(client, sessionId);
        var grade = await EssayGradingTestData.ReadGradeForAsync(factory, sessionId, essayId);

        await EssayGradingTestData.GradeAsync(factory, grade.Id);

        var body = await client.GetFromJsonAsync<JsonElement>($"{ExamTestData.Route}/{sessionId}", CancellationToken);
        (body.GetProperty("scorePercent").GetDecimal(), body.GetProperty("isPassed").GetBoolean()).Should().Be((100m, true));
    }

    private async Task<(HttpClient Client, Guid SessionId, Guid McqId, Guid EssayId)> StartEssayExamAsync()
    {
        var (unitId, mcqId, essayId) = await EssayGradingTestData.SeedEssayUnitExamAsync(factory);
        var (_, client) = await SignedInStudentAsync(factory);
        var started = await StartAsync(client, unitId);
        return (client, started.GetProperty("id").GetGuid(), mcqId, essayId);
    }

    private static Task<HttpResponseMessage> SaveEssayAsync(HttpClient client, Guid sessionId, Guid questionId, string text)
    {
        return client.PutAsJsonAsync($"{ExamTestData.Route}/{sessionId}/answers/{questionId}", new { answer = new { text } }, CancellationToken);
    }
}
