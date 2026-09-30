using Elmanhg.Domain.MathStepGrading;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.MathStepGrading;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Exams.ExamTestData;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Exams;

public sealed class MathStepGradingExamEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Submit_StepGradedMathAnswer_RequestsMathStepGradeWithoutAttempt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (unitId, mcqId, mathId) = await MathStepGradingTestData.SeedMathUnitExamAsync(factory);
        var (_, client) = await SignedInStudentAsync(factory);
        var sessionId = (await StartAsync(client, unitId)).GetProperty("id").GetGuid();
        using var mcq = await SaveAsync(client, sessionId, mcqId, "b");
        using var math = await client.PutAsJsonAsync($"{ExamTestData.Route}/{sessionId}/answers/{mathId}", new { answer = new { steps = new[] { "2x = 4" }, finalAnswer = "x=2" } }, cancellationToken);

        using var response = await SubmitAsync(client, sessionId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        body.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("questionId").GetGuid() == mathId).GetProperty("attempt").ValueKind.Should().Be(JsonValueKind.Null);
        var grade = (await MathStepGradingTestData.ReadGradesForAsync(factory, sessionId)).Should().ContainSingle().Subject;
        (grade.Status, grade.QuestionId, grade.TimeTakenMilliseconds).Should().Be((MathStepGradeStatus.Pending, mathId, 0));
        (await ReadAttemptsAsync(factory, sessionId)).Should().ContainSingle().Which.QuestionId.Should().Be(mcqId);
    }
}
