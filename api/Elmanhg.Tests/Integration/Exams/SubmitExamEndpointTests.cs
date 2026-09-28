using Elmanhg.Domain.Mastery;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Exams.ExamTestData;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Exams;

public sealed class SubmitExamEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Post_Submit_GradesSavedAnswersAndReturnsBreakdown()
    {
        var (_, client, sessionId, questionIds) = await StartWithFirstAnsweredAsync();

        using var response = await SubmitAsync(client, sessionId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("scorePercent").GetDecimal().Should().Be(50m);
        body.GetProperty("isPassed").GetBoolean().Should().BeTrue();
        body.GetProperty("lessons")[0].GetProperty("scorePercent").GetDecimal().Should().Be(50m);
        body.GetProperty("items").EnumerateArray().Should().AllSatisfy(x => x.GetProperty("correctAnswer").ValueKind.Should().Be(JsonValueKind.Object));
        (await ReadAttemptsAsync(factory, sessionId)).Should().ContainSingle().Which.QuestionId.Should().Be(questionIds[0]);
    }

    [Fact]
    public async Task Post_SubmitTwice_ReturnsSameResult()
    {
        var (_, client, sessionId, _) = await StartWithFirstAnsweredAsync();
        using var first = await SubmitAsync(client, sessionId);

        using var second = await SubmitAsync(client, sessionId);

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await second.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("scorePercent").GetDecimal().Should().Be(50m);
        (await ReadAttemptsAsync(factory, sessionId)).Should().HaveCount(1);
    }

    [Fact]
    public async Task Post_Submit_UpdatesMastery()
    {
        var (studentId, client, sessionId, questionIds) = await StartWithFirstAnsweredAsync();

        using var response = await SubmitAsync(client, sessionId);

        (await ReadMasteriesAsync(studentId)).Should().ContainSingle().Which.QuestionId.Should().Be(questionIds[0]);
    }

    [Fact]
    public async Task Post_SubmitAfterDeadline_GradesSavedAnswers()
    {
        var (_, client, sessionId, _) = await StartWithFirstAnsweredAsync();
        await ExpireAsync(factory, sessionId, TimeSpan.FromHours(1));

        using var response = await SubmitAsync(client, sessionId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAttemptsAsync(factory, sessionId)).Should().HaveCount(1);
    }

    [Fact]
    public async Task Post_TestModeExam_DoesNotWriteMastery()
    {
        var (_, unitId, _, _) = await SeedExamUnitAsync(factory, 1);
        var admin = await ScopeTestData.SeedAdminAsync(factory, TestContext.Current.CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, admin, TestContext.Current.CancellationToken);
        var started = await StartAsync(client, unitId);
        var sessionId = started.GetProperty("id").GetGuid();
        using var save = await SaveAsync(client, sessionId, ItemQuestionIds(started)[0], "b");

        using var response = await SubmitAsync(client, sessionId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAttemptsAsync(factory, sessionId)).Should().HaveCount(1);
        (await ReadMasteriesAsync(admin.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Post_OtherStudent_Returns404()
    {
        var (_, _, sessionId, _) = await StartWithFirstAnsweredAsync();
        var (_, other) = await SignedInStudentAsync(factory);

        using var response = await SubmitAsync(other, sessionId);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("SESSION_NOT_FOUND");
        (await ReadSessionAsync(factory, sessionId)).SubmittedAt.Should().BeNull();
    }

    [Fact]
    public async Task Post_Anonymous_Returns401()
    {
        using var client = AuthTestClient.Create(factory);

        using var response = await SubmitAsync(client, Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<(Guid StudentId, HttpClient Client, Guid SessionId, List<Guid> QuestionIds)> StartWithFirstAnsweredAsync()
    {
        var (_, unitId, _, _) = await SeedExamUnitAsync(factory, 2).ConfigureAwait(false);
        var (student, client) = await SignedInStudentAsync(factory).ConfigureAwait(false);
        var body = await StartAsync(client, unitId).ConfigureAwait(false);
        var sessionId = body.GetProperty("id").GetGuid();
        var questionIds = ItemQuestionIds(body);
        using var save = await SaveAsync(client, sessionId, questionIds[0], "b").ConfigureAwait(false);
        save.StatusCode.Should().Be(HttpStatusCode.OK);
        return (student.Id, client, sessionId, questionIds);
    }

    private async Task<List<QuestionMastery>> ReadMasteriesAsync(Guid studentId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.QuestionMasteries.AsNoTracking().Where(x => x.StudentId == studentId).ToListAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
    }
}
