using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.TeacherInbox;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Dashboard.DashboardTestData;

namespace Elmanhg.Tests.Integration.Dashboard;

public sealed class AskTeacherMetricsEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_SubjectFilter_CountsOpenOverdueAndBreaches()
    {
        var now = TimeProvider.System.GetUtcNow();
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Geology", 1, CancellationToken);
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var first = await TeacherInboxTestData.SeedThreadAsync(factory, new TeacherThreadBuilder().ForStudent(student.Id).WithContext(TeacherInboxTestData.ContextFor(subjectId)).SubmittedAt(now.AddHours(-30)).Build());
        await TeacherInboxTestData.SeedThreadAsync(factory, new TeacherThreadBuilder().ForStudent(student.Id).WithContext(TeacherInboxTestData.ContextFor(subjectId)).SubmittedAt(now.AddHours(-30)).Build());
        await SeedBreachAsync(factory, first, now);
        using var client = await AdminClientAsync(factory);

        var body = await GetJsonAsync(client, $"ask-teacher?subjectId={subjectId}");

        body.GetProperty("openThreads").GetInt32().Should().Be(2);
        body.GetProperty("awaitingReply").GetInt32().Should().Be(2);
        body.GetProperty("overdueNow").GetInt32().Should().Be(2);
        body.GetProperty("slaBreaches").GetInt32().Should().Be(1);
        body.GetProperty("replies").GetInt32().Should().Be(0);
        body.GetProperty("slaComplianceRate").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Get_SubjectFilter_ReplyWithinSla_CountsCompliance()
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Astronomy", 1, CancellationToken);
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var (_, teacherClient) = await TeacherInboxTestData.SignedInTeacherForAsync(factory, subjectId);
        var thread = await TeacherInboxTestData.SeedThreadAsync(factory, new TeacherThreadBuilder().ForStudent(student.Id).WithContext(TeacherInboxTestData.ContextFor(subjectId)).SubmittedAt(TimeProvider.System.GetUtcNow().AddHours(-1)).Build());
        using var claim = await teacherClient.PostAsync($"{TeacherInboxTestData.Route}/{thread.Id}/claim", null, CancellationToken);
        using var reply = await teacherClient.PostAsJsonAsync($"{TeacherInboxTestData.Route}/{thread.Id}/replies", new { text = "Because force equals mass times acceleration." }, CancellationToken);
        using var client = await AdminClientAsync(factory);

        var body = await GetJsonAsync(client, $"ask-teacher?subjectId={subjectId}");

        (claim.StatusCode, reply.StatusCode).Should().Be((HttpStatusCode.OK, HttpStatusCode.OK));
        body.GetProperty("replies").GetInt32().Should().Be(1);
        body.GetProperty("repliedWithinSla").GetInt32().Should().Be(1);
        body.GetProperty("slaComplianceRate").GetDecimal().Should().Be(1m);
        body.GetProperty("medianReplySeconds").GetInt64().Should().BeGreaterThan(0);
    }
}
