using Elmanhg.Domain.Sessions;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.Exams;

public static class ExamAttemptsTestData
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public static async Task<Guid> InsertExamSittingAsync(ApiFactory factory, Guid studentId, SessionKind kind, string scopeJson, string scopeKey, decimal? scorePercent, bool submitted, bool isTestMode, DateTimeOffset startedAt)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = Guid.NewGuid();
        var kindName = kind.ToString();
        DateTimeOffset? submittedAt = submitted ? startedAt.AddMinutes(30) : null;
        await context.Database.ExecuteSqlAsync($"INSERT INTO \"Sessions\" (\"Id\",\"CreatedBy\",\"CreationDate\",\"IsDeleted\",\"IsTestMode\",\"Kind\",\"LastActivityAt\",\"PassMark\",\"Scope\",\"ScopeKey\",\"ScorePercent\",\"StartedAt\",\"StudentId\",\"SubmittedAt\",\"UpdationDate\") VALUES ({id},{studentId},{startedAt},false,{isTestMode},{kindName},{startedAt},50,CAST({scopeJson} AS jsonb),{scopeKey},{scorePercent},{startedAt},{studentId},{submittedAt},{startedAt})", CancellationToken).ConfigureAwait(false);
        return id;
    }

    public static Task<Guid> InsertUnitSittingAsync(ApiFactory factory, Guid studentId, Guid unitId, decimal? score, bool submitted = true, bool isTestMode = false, DateTimeOffset startedAt = default)
    {
        var examScope = new UnitExamScope(unitId);
        return InsertExamSittingAsync(factory, studentId, SessionKind.UnitExam, examScope.ToJson(), examScope.ToKey(), score, submitted, isTestMode, startedAt);
    }

    public static Task<Guid> InsertMultiSittingAsync(ApiFactory factory, Guid studentId, Guid subjectId, IReadOnlyList<Guid> unitIds, int size, decimal? score, DateTimeOffset startedAt)
    {
        var examScope = new MultiUnitExamScope(subjectId, unitIds, size);
        return InsertExamSittingAsync(factory, studentId, SessionKind.MultiUnitExam, examScope.ToJson(), examScope.ToKey(), score, submitted: true, isTestMode: false, startedAt);
    }

    public static async Task<JsonElement> GetAttemptsAsync(HttpClient client, string relativePath)
    {
        using var response = await client.GetAsync($"{ExamTestData.Route}/{relativePath}", CancellationToken).ConfigureAwait(false);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken).ConfigureAwait(false);
    }
}
