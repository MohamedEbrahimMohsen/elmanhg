using Elmanhg.Domain.Sessions;
using Elmanhg.Infrastructure.AiService;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.MathStepGrading;

public sealed class MathStepGradeEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task GetMathStepGrade_Pending_ReturnsPendingWithoutScore()
    {
        var (student, client) = await SignedInStudentAsync(factory);
        var (_, sessionId, questionId) = await MathStepGradingTestData.SeedPendingAsync(factory, student.Id);

        using var response = await client.GetAsync(MathStepGradingTestData.Route(sessionId, questionId), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        (body.GetProperty("status").GetString(), body.GetProperty("score").ValueKind, body.GetProperty("maxScore").GetInt32(), body.GetProperty("steps").GetArrayLength()).Should().Be(("Pending", JsonValueKind.Null, 2, 0));
    }

    [Fact]
    public async Task GetMathStepGrade_AfterWorkerCommands_ReturnsGradedStepsAndJustification()
    {
        var (student, client) = await SignedInStudentAsync(factory);
        var (gradeId, sessionId, questionId) = await MathStepGradingTestData.SeedPendingAsync(factory, student.Id);
        await MathStepGradingTestData.GradeAsync(factory, gradeId);

        using var response = await client.GetAsync(MathStepGradingTestData.Route(sessionId, questionId), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        (body.GetProperty("status").GetString(), body.GetProperty("score").GetDecimal(), body.GetProperty("outcome").GetString(), body.GetProperty("finalAnswerVerdict").GetString(), body.GetProperty("justification").GetString()).Should().Be(("Graded", 2m, "Correct", "Equivalent", FakeAiMathStepGradingClient.FakeJustification));
        var step = body.GetProperty("steps")[1];
        (step.GetProperty("stepIndex").GetInt32(), step.GetProperty("step").GetString(), step.GetProperty("points").GetInt32(), step.GetProperty("maxPoints").GetInt32()).Should().Be((1, "x = 2", 2, 2));
        (await ReadAttemptsAsync(factory, sessionId)).Should().ContainSingle().Which.GradedBy.Should().Be(AttemptGrader.AI);
    }

    [Fact]
    public async Task GetMathStepGrade_OtherStudent_Returns404()
    {
        var (student, _) = await SignedInStudentAsync(factory);
        var (_, sessionId, questionId) = await MathStepGradingTestData.SeedPendingAsync(factory, student.Id);
        var (_, other) = await SignedInStudentAsync(factory);

        using var response = await other.GetAsync(MathStepGradingTestData.Route(sessionId, questionId), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("code").GetString().Should().Be("MATH_STEP_GRADE_NOT_FOUND");
    }

    [Fact]
    public async Task GetMathStepGrade_Teacher_Returns403()
    {
        var (student, _) = await SignedInStudentAsync(factory);
        var (_, sessionId, questionId) = await MathStepGradingTestData.SeedPendingAsync(factory, student.Id);
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, TestContext.Current.CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, TestContext.Current.CancellationToken);

        using var response = await client.GetAsync(MathStepGradingTestData.Route(sessionId, questionId), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetMathStepGrade_Anonymous_Returns401()
    {
        using var anonymous = AuthTestClient.Create(factory);

        using var response = await anonymous.GetAsync(MathStepGradingTestData.Route(Guid.NewGuid(), Guid.NewGuid()), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
