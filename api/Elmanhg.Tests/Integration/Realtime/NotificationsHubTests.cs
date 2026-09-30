using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.GradeReviews;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.TeacherInbox.TeacherInboxTestData;

namespace Elmanhg.Tests.Integration.Realtime;

public sealed class NotificationsHubTests(ApiFactory factory)
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Hub_StudentConnected_ReceivesTeacherReply()
    {
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, $"Physics {Guid.NewGuid():N}", CancellationToken);
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        using var studentClient = await ScopeTestData.SignedInClientAsync(factory, student, CancellationToken);
        var (teacher, teacherClient) = await SignedInTeacherForAsync(factory, subjectId);
        var thread = await SeedThreadAsync(factory, new TeacherThreadBuilder().ForStudent(student.Id).WithContext(ContextFor(subjectId)).ClaimedBy(teacher.Id).Build());
        var received = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var connection = Connect(studentClient.DefaultRequestHeaders.Authorization!.Parameter);
        connection.On<JsonElement>("teacherReplyReceived", payload => received.TrySetResult(payload));
        await connection.StartAsync(CancellationToken);

        using var response = await teacherClient.PostAsJsonAsync($"{Route}/{thread.Id}/replies", new { text = "Because F = ma." }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await received.Task.WaitAsync(WaitLimit, CancellationToken);
        payload.GetProperty("threadId").GetGuid().Should().Be(thread.Id);
    }

    [Fact]
    public async Task Hub_StudentConnected_ReceivesGradeReviewed()
    {
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, $"Physics {Guid.NewGuid():N}", CancellationToken);
        var (gradeId, sessionId, questionId, student) = await GradeReviewTestData.SeedInReviewEssayAsync(factory, subjectId);
        using var studentClient = await ScopeTestData.SignedInClientAsync(factory, student, CancellationToken);
        var (_, teacherClient) = await SignedInTeacherForAsync(factory, subjectId);
        var received = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var connection = Connect(studentClient.DefaultRequestHeaders.Authorization!.Parameter);
        connection.On<JsonElement>("gradeReviewed", payload => received.TrySetResult(payload));
        await connection.StartAsync(CancellationToken);

        using var response = await teacherClient.PostAsJsonAsync(GradeReviewTestData.EssayRoute(subjectId, gradeId), new { decision = "Overridden", score = 4, comment = "Full marks for the definition." }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await received.Task.WaitAsync(WaitLimit, CancellationToken);
        (payload.GetProperty("sessionId").GetGuid(), payload.GetProperty("questionId").GetGuid()).Should().Be((sessionId, questionId));
    }

    [Fact]
    public async Task Hub_Anonymous_IsRejected()
    {
        await using var connection = Connect(null);

        var act = () => connection.StartAsync(CancellationToken);

        (await act.Should().ThrowAsync<HttpRequestException>()).Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private HubConnection Connect(string? accessToken)
    {
        return new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/api/hubs/notifications"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult(accessToken);
            })
            .Build();
    }
}
