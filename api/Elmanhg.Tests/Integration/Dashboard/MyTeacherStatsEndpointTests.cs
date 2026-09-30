using Elmanhg.Domain.Identity;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.QuestionValidation;
using Elmanhg.Tests.Integration.TeacherInbox;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Dashboard.DashboardTestData;

namespace Elmanhg.Tests.Integration.Dashboard;

public sealed class MyTeacherStatsEndpointTests(ApiFactory factory)
{
    private const string MyStats = "my-stats";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_Teacher_CountsOnlyOwnDecisions()
    {
        var (subjectId, _, lessonId) = await ValidationTestData.SeedSubjectTreeAsync(factory, "Zoology");
        var (_, teacherA) = await ValidationTestData.SeedAssignedTeacherAsync(factory, subjectId);
        var (_, teacherB) = await ValidationTestData.SeedAssignedTeacherAsync(factory, subjectId);
        var approvedByA = await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: false, CancellationToken);
        var rejectedByA = await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: false, CancellationToken);
        var approvedByB = await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: false, CancellationToken);
        using var approveA = await teacherA.PostAsJsonAsync($"{ValidationTestData.Route}/questions/{approvedByA}/approve", new { version = 1 }, CancellationToken);
        using var rejectA = await teacherA.PostAsJsonAsync($"{ValidationTestData.Route}/questions/{rejectedByA}/reject", new { version = 1, reason = "Wrong unit" }, CancellationToken);
        using var approveB = await teacherB.PostAsJsonAsync($"{ValidationTestData.Route}/questions/{approvedByB}/approve", new { version = 1 }, CancellationToken);

        var bodyA = await GetJsonAsync(teacherA, MyStats);
        var bodyB = await GetJsonAsync(teacherB, MyStats);

        (approveA.StatusCode, rejectA.StatusCode, approveB.StatusCode).Should().Be((HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK));
        (bodyA.GetProperty("approved").GetInt32(), bodyA.GetProperty("rejected").GetInt32()).Should().Be((1, 1));
        bodyA.GetProperty("medianSecondsToDecision").ValueKind.Should().Be(JsonValueKind.Number);
        (bodyB.GetProperty("approved").GetInt32(), bodyB.GetProperty("rejected").GetInt32()).Should().Be((1, 0));
    }

    [Fact]
    public async Task Get_Teacher_CountsOwnRepliesWithinSla()
    {
        var (subjectId, _, _) = await ValidationTestData.SeedSubjectTreeAsync(factory, "Botany");
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var (_, teacherA) = await TeacherInboxTestData.SignedInTeacherForAsync(factory, subjectId);
        var (_, teacherB) = await ValidationTestData.SeedAssignedTeacherAsync(factory, subjectId);
        var thread = await TeacherInboxTestData.SeedThreadAsync(factory, new TeacherThreadBuilder().ForStudent(student.Id).WithContext(TeacherInboxTestData.ContextFor(subjectId)).SubmittedAt(TimeProvider.System.GetUtcNow().AddHours(-1)).Build());
        using var claim = await teacherA.PostAsync($"{TeacherInboxTestData.Route}/{thread.Id}/claim", null, CancellationToken);
        using var reply = await teacherA.PostAsJsonAsync($"{TeacherInboxTestData.Route}/{thread.Id}/replies", new { text = "Plants make food by photosynthesis." }, CancellationToken);

        var bodyA = await GetJsonAsync(teacherA, MyStats);
        var bodyB = await GetJsonAsync(teacherB, MyStats);

        (claim.StatusCode, reply.StatusCode).Should().Be((HttpStatusCode.OK, HttpStatusCode.OK));
        bodyA.GetProperty("replies").GetInt32().Should().Be(1);
        bodyA.GetProperty("repliedWithinSla").GetInt32().Should().Be(1);
        bodyA.GetProperty("slaComplianceRate").GetDecimal().Should().Be(1m);
        bodyA.GetProperty("medianReplySeconds").GetInt64().Should().BeGreaterThan(0);
        bodyB.GetProperty("replies").GetInt32().Should().Be(0);
        bodyB.GetProperty("slaComplianceRate").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Get_NoActivity_ReturnsZerosNullsAndThirtyDayRange()
    {
        using var client = await TeacherClientAsync();

        var body = await GetJsonAsync(client, MyStats);

        body.GetProperty("approved").GetInt32().Should().Be(0);
        body.GetProperty("replies").GetInt32().Should().Be(0);
        body.GetProperty("medianSecondsToDecision").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("slaComplianceRate").ValueKind.Should().Be(JsonValueKind.Null);
        var from = DateOnly.Parse(body.GetProperty("from").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        var to = DateOnly.Parse(body.GetProperty("to").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        (to.DayNumber - from.DayNumber).Should().Be(29);
        body.GetProperty("generatedAt").ValueKind.Should().Be(JsonValueKind.String);
    }

    [Fact]
    public async Task Get_FromAfterTo_Returns422DashboardDateRangeInvalid()
    {
        using var client = await TeacherClientAsync();

        using var response = await client.GetAsync($"{Route}/{MyStats}?from=2026-01-10&to=2026-01-05", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ValidationTestData.ReadCodeAsync(response)).Should().Be("DASHBOARD_DATE_RANGE_INVALID");
    }

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        using var client = AuthTestClient.Create(factory);

        using var response = await client.GetAsync($"{Route}/{MyStats}", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.Student)]
    [InlineData(UserRole.Admin)]
    public async Task Get_NonTeacher_Returns403(UserRole role)
    {
        var user = role == UserRole.Student ? await ScopeTestData.SeedStudentAsync(factory, CancellationToken) : await ScopeTestData.SeedAdminAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, user, CancellationToken);

        using var response = await client.GetAsync($"{Route}/{MyStats}", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<HttpClient> TeacherClientAsync()
    {
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken).ConfigureAwait(false);
        return await ScopeTestData.SignedInClientAsync(factory, teacher, CancellationToken).ConfigureAwait(false);
    }
}
