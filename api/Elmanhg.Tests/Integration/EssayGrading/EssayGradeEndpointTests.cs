using Elmanhg.Infrastructure.AiService;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.EssayGrading;

public sealed class EssayGradeEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task GetEssayGrade_Pending_ReturnsPendingWithoutScore()
    {
        var (student, client) = await SignedInStudentAsync(factory);
        var (_, sessionId, questionId) = await EssayGradingTestData.SeedPendingAsync(factory, student.Id);

        using var response = await client.GetAsync(EssayGradeRoute(sessionId, questionId), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        (body.GetProperty("status").GetString(), body.GetProperty("score").ValueKind, body.GetProperty("maxScore").GetInt32()).Should().Be(("Pending", JsonValueKind.Null, 5));
    }

    [Fact]
    public async Task GetEssayGrade_AfterGrading_ReturnsGradedScoreCriteriaAndJustification()
    {
        var (student, client) = await SignedInStudentAsync(factory);
        var (gradeId, sessionId, questionId) = await EssayGradingTestData.SeedPendingAsync(factory, student.Id);
        await EssayGradingTestData.GradeAsync(factory, gradeId);

        using var response = await client.GetAsync(EssayGradeRoute(sessionId, questionId), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        (body.GetProperty("status").GetString(), body.GetProperty("score").GetDecimal(), body.GetProperty("outcome").GetString(), body.GetProperty("justification").GetString()).Should().Be(("Graded", 5m, "Correct", FakeAiEssayGradingClient.FakeJustification));
        var criterion = body.GetProperty("criteria")[0];
        (criterion.GetProperty("criterionId").GetString(), criterion.GetProperty("points").GetInt32(), criterion.GetProperty("maxPoints").GetInt32()).Should().Be(("c1", 2, 2));
        var grade = await EssayGradingTestData.ReadAsync(factory, gradeId);
        (grade.Model, grade.Confidence, grade.Attempts, grade.CostUsd).Should().Be(("fake", (decimal?)0.9m, 1, (decimal?)0m));
    }

    [Fact]
    public async Task GetEssayGrade_OtherStudent_Returns404EssayGradeNotFound()
    {
        var (student, _) = await SignedInStudentAsync(factory);
        var (_, sessionId, questionId) = await EssayGradingTestData.SeedPendingAsync(factory, student.Id);
        var (_, other) = await SignedInStudentAsync(factory);

        using var response = await other.GetAsync(EssayGradeRoute(sessionId, questionId), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("code").GetString().Should().Be("ESSAY_GRADE_NOT_FOUND");
    }

    [Fact]
    public async Task GetEssayGrade_Teacher_Returns403()
    {
        var (student, _) = await SignedInStudentAsync(factory);
        var (_, sessionId, questionId) = await EssayGradingTestData.SeedPendingAsync(factory, student.Id);
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, TestContext.Current.CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, TestContext.Current.CancellationToken);

        using var response = await client.GetAsync(EssayGradeRoute(sessionId, questionId), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetEssayGrade_Anonymous_Returns401()
    {
        using var anonymous = AuthTestClient.Create(factory);

        using var response = await anonymous.GetAsync(EssayGradeRoute(Guid.NewGuid(), Guid.NewGuid()), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static string EssayGradeRoute(Guid sessionId, Guid questionId) => $"{Route}/{sessionId}/questions/{questionId}/essay-grade";
}
