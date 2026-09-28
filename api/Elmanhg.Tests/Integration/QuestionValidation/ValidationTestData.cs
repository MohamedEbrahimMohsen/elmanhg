using Elmanhg.Domain.Identity;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Builders.QuestionBuilder;

namespace Elmanhg.Tests.Integration.QuestionValidation;

public static class ValidationTestData
{
    public const string Route = "/api/validation-queue";

    public static async Task<(User Teacher, HttpClient Client)> SeedAssignedTeacherAsync(ApiFactory factory, Guid subjectId)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, cancellationToken).ConfigureAwait(false);
        await ScopeTestData.AssignAsync(factory, teacher.Id, subjectId, cancellationToken).ConfigureAwait(false);
        var client = await ScopeTestData.SignedInClientAsync(factory, teacher, cancellationToken).ConfigureAwait(false);
        return (teacher, client);
    }

    public static async Task<(Guid SubjectId, Guid UnitId, Guid LessonId)> SeedSubjectTreeAsync(ApiFactory factory, string name)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, name, 1, cancellationToken).ConfigureAwait(false);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, cancellationToken).ConfigureAwait(false);
        var lessonId = await ContentTestData.SeedLessonAsync(factory, unitId, "Newton's laws", 1, ["State the first law"], cancellationToken).ConfigureAwait(false);
        return (subjectId, unitId, lessonId);
    }

    public static async Task<Guid> StartSessionAsync(HttpClient client)
    {
        using var response = await client.PostAsync($"{Route}/review-sessions", null, TestContext.Current.CancellationToken).ConfigureAwait(false);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return body.GetProperty("reviewSessionId").GetGuid();
    }

    public static async Task<HttpClient> AdminClientAsync(ApiFactory factory)
    {
        var admin = await ScopeTestData.SeedAdminAsync(factory, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return await ScopeTestData.SignedInClientAsync(factory, admin, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    public static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return body.GetProperty("code").GetString();
    }

    public static async Task<List<JsonElement>> ReadItemsAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return body.GetProperty("items").EnumerateArray().ToList();
    }

    public static object McqRequest(string stem)
    {
        return new { type = "Mcq", stem, body = Json("""{"options":[{"id":"a","text":"3"},{"id":"b","text":"4"}]}"""), gradingSpec = Json("""{"correctOptionId":"b"}"""), explanation = "<p>Add the numbers.</p>", difficulty = "Medium", maxScore = 1 };
    }
}
