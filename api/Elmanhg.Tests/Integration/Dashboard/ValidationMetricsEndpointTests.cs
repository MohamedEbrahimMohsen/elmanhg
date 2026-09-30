using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.QuestionValidation;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Dashboard.DashboardTestData;

namespace Elmanhg.Tests.Integration.Dashboard;

public sealed class ValidationMetricsEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_SubjectFilter_ReturnsBacklogDecisionsAndTeacherThroughput()
    {
        var (subjectId, _, lessonId) = await ValidationTestData.SeedSubjectTreeAsync(factory, "Biology");
        var (teacher, teacherClient) = await ValidationTestData.SeedAssignedTeacherAsync(factory, subjectId);
        var approvedId = await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: false, CancellationToken);
        var rejectedId = await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: false, CancellationToken);
        await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: false, CancellationToken);
        using var approve = await teacherClient.PostAsJsonAsync($"{ValidationTestData.Route}/questions/{approvedId}/approve", new { version = 1 }, CancellationToken);
        using var reject = await teacherClient.PostAsJsonAsync($"{ValidationTestData.Route}/questions/{rejectedId}/reject", new { version = 1, reason = "Wrong unit" }, CancellationToken);
        using var client = await AdminClientAsync(factory);

        var body = await GetJsonAsync(client, $"validation?subjectId={subjectId}");

        (approve.StatusCode, reject.StatusCode).Should().Be((HttpStatusCode.OK, HttpStatusCode.OK));
        body.GetProperty("pendingBacklog").GetInt32().Should().Be(1);
        body.GetProperty("approved").GetInt32().Should().Be(1);
        body.GetProperty("rejected").GetInt32().Should().Be(1);
        body.GetProperty("medianSecondsToDecision").ValueKind.Should().Be(JsonValueKind.Number);
        var row = body.GetProperty("byTeacher").EnumerateArray().Should().ContainSingle().Subject;
        row.GetProperty("teacherId").GetGuid().Should().Be(teacher.Id);
        row.GetProperty("displayName").GetString().Should().Be(teacher.DisplayName);
        (row.GetProperty("approved").GetInt32(), row.GetProperty("rejected").GetInt32()).Should().Be((1, 1));
    }
}
