using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Elmanhg.Tests.Integration.AuditLogs;

public sealed class AuditLogsEndpointTests(ApiFactory factory)
{
    private const string Route = "/api/audit-logs";

    [Fact]
    public async Task Get_AfterAssign_ReturnsSuccessEntryWithCreatedDiff()
    {
        var arranged = await ArrangeAsync();
        await AssignAsync(arranged, arranged.SubjectId, HttpStatusCode.OK);

        var body = await GetOkAsync(arranged, $"actor={Escape(arranged.AdminEmail)}");

        var item = body.GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
        item.GetProperty("action").GetString().Should().Be("Teacher.AssignSubject");
        item.GetProperty("resourceType").GetString().Should().Be("Teacher");
        item.GetProperty("resourceId").GetGuid().Should().Be(arranged.TeacherId);
        item.GetProperty("outcome").GetString().Should().Be("Success");
        item.GetProperty("actorRole").GetString().Should().Be("Admin");
        var diff = ParseDiff(item);
        diff[0]!["change"]!.GetValue<string>().Should().Be("Created");
        diff[0]!["properties"]!["subjectId"]!["after"]!.GetValue<string>().Should().Be(arranged.SubjectId.ToString());
    }

    [Fact]
    public async Task Get_AfterUnassign_ReturnsEntryWithIsDeletedDiff()
    {
        var arranged = await ArrangeAsync();
        await AssignAsync(arranged, arranged.SubjectId, HttpStatusCode.OK);
        using (var unassigned = await arranged.Admin.DeleteAsync(TeacherRoute(arranged.TeacherId, arranged.SubjectId), TestContext.Current.CancellationToken))
        {
            unassigned.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var body = await GetOkAsync(arranged, $"actor={Escape(arranged.AdminEmail)}");

        var newest = body.GetProperty("items")[0];
        newest.GetProperty("action").GetString().Should().Be("Teacher.UnassignSubject");
        var isDeleted = ParseDiff(newest)[0]!["properties"]!["isDeleted"]!;
        isDeleted["before"]!.GetValue<bool>().Should().BeFalse();
        isDeleted["after"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact]
    public async Task Get_AfterFailedAssign_ReturnsFailureEntryWithErrorCode()
    {
        var arranged = await ArrangeAsync();
        await AssignAsync(arranged, Guid.NewGuid(), HttpStatusCode.NotFound);

        var body = await GetOkAsync(arranged, $"actor={Escape(arranged.AdminEmail)}");

        var item = body.GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
        item.GetProperty("outcome").GetString().Should().Be("Failure");
        item.GetProperty("errorCode").GetString().Should().Be("SUBJECT_NOT_FOUND");
        item.GetProperty("diff").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Get_ResourceTypeFilter_ExcludesOtherTypes()
    {
        var arranged = await ArrangeAsync();
        await AssignAsync(arranged, arranged.SubjectId, HttpStatusCode.OK);

        var body = await GetOkAsync(arranged, $"resourceType=Question&actor={Escape(arranged.AdminEmail)}");

        body.GetProperty("totalItems").GetInt64().Should().Be(0);
    }

    [Fact]
    public async Task Get_DateRangeInFuture_ReturnsNoItems()
    {
        var arranged = await ArrangeAsync();
        await AssignAsync(arranged, arranged.SubjectId, HttpStatusCode.OK);

        var body = await GetOkAsync(arranged, $"from={Escape(DateTimeOffset.UtcNow.AddDays(1).ToString("O", CultureInfo.InvariantCulture))}");

        body.GetProperty("totalItems").GetInt64().Should().Be(0);
    }

    [Fact]
    public async Task Get_PageSizeOne_ReturnsNewestFirstWithTotals()
    {
        var arranged = await ArrangeAsync();
        var secondSubjectId = await ScopeTestData.SeedSubjectAsync(factory, "Chemistry", TestContext.Current.CancellationToken);
        await AssignAsync(arranged, arranged.SubjectId, HttpStatusCode.OK);
        await AssignAsync(arranged, secondSubjectId, HttpStatusCode.OK);

        var body = await GetOkAsync(arranged, $"actor={Escape(arranged.AdminEmail)}&pageSize=1");

        var item = body.GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
        ParseDiff(item)[0]!["properties"]!["subjectId"]!["after"]!.GetValue<string>().Should().Be(secondSubjectId.ToString());
        body.GetProperty("totalItems").GetInt64().Should().Be(2);
        body.GetProperty("totalPages").GetInt64().Should().Be(2);
    }

    [Fact]
    public async Task Get_ToBeforeFrom_Returns422DateRangeInvalid()
    {
        var arranged = await ArrangeAsync();

        using var response = await arranged.Admin.GetAsync($"{Route}?from=2026-02-01T00:00:00Z&to=2026-01-01T00:00:00Z", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("AUDIT_LOG_DATE_RANGE_INVALID");
    }

    [Fact]
    public async Task Get_PageSizeAboveMax_Returns422PageSizeInvalid()
    {
        var arranged = await ArrangeAsync();

        using var response = await arranged.Admin.GetAsync($"{Route}?pageSize=101", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("AUDIT_LOG_PAGE_SIZE_INVALID");
    }

    [Fact]
    public async Task Get_TeacherCaller_Returns403()
    {
        using var client = await TeacherClientAsync();

        using var response = await client.GetAsync(Route, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        using var client = AuthTestClient.Create(factory);

        using var response = await client.GetAsync(Route, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetResourceTypes_AfterAssign_ContainsTeacher()
    {
        var arranged = await ArrangeAsync();
        await AssignAsync(arranged, arranged.SubjectId, HttpStatusCode.OK);

        using var response = await arranged.Admin.GetAsync($"{Route}/resource-types", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var types = await response.Content.ReadFromJsonAsync<List<string>>(TestContext.Current.CancellationToken);
        types.Should().Contain("Teacher").And.BeInAscendingOrder(StringComparer.Ordinal);
    }

    [Fact]
    public async Task GetResourceTypes_TeacherCaller_Returns403()
    {
        using var client = await TeacherClientAsync();

        using var response = await client.GetAsync($"{Route}/resource-types", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private sealed record Arranged(HttpClient Admin, string AdminEmail, Guid TeacherId, Guid SubjectId);

    private static string Escape(string value) => Uri.EscapeDataString(value);

    private static string TeacherRoute(Guid teacherId, Guid subjectId) => $"/api/teachers/{teacherId}/subjects/{subjectId}";

    private static JsonNode ParseDiff(JsonElement item) => JsonNode.Parse(item.GetProperty("diff").GetString()!)!;

    private async Task<Arranged> ArrangeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, cancellationToken).ConfigureAwait(false);
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, "Physics", cancellationToken).ConfigureAwait(false);
        var admin = await ScopeTestData.SeedAdminAsync(factory, cancellationToken).ConfigureAwait(false);
        var client = await ScopeTestData.SignedInClientAsync(factory, admin, cancellationToken).ConfigureAwait(false);
        return new Arranged(client, admin.Email!, teacher.Id, subjectId);
    }

    private static async Task AssignAsync(Arranged arranged, Guid subjectId, HttpStatusCode expected)
    {
        using var response = await arranged.Admin.PostAsync(TeacherRoute(arranged.TeacherId, subjectId), null, TestContext.Current.CancellationToken).ConfigureAwait(false);
        response.StatusCode.Should().Be(expected);
    }

    private static async Task<JsonElement> GetOkAsync(Arranged arranged, string query)
    {
        using var response = await arranged.Admin.GetAsync($"{Route}?{query}", TestContext.Current.CancellationToken).ConfigureAwait(false);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpClient> TeacherClientAsync()
    {
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return await ScopeTestData.SignedInClientAsync(factory, teacher, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return body.GetProperty("code").GetString();
    }
}
