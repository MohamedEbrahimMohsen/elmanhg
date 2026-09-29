using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Exams;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Avatar.AvatarTestData;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Avatar;

public sealed class AvatarStatusEndpointTests(ApiFactory factory)
{
    private const string StatusRoute = $"{AvatarTestData.Route}/status";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetStatus_FreeStudentWithTwoMessages_ReturnsCounts()
    {
        var (student, client) = await SignedInFreeStudentAsync(factory);
        await SeedUsageAsync(factory, student.Id, 2, DateTimeOffset.UtcNow);

        var body = await client.GetFromJsonAsync<JsonElement>(StatusRoute, CancellationToken);

        body.GetProperty("examInProgress").GetBoolean().Should().BeFalse();
        body.GetProperty("tier").GetString().Should().Be("Free");
        (body.GetProperty("dailyMessageLimit").GetInt32(), body.GetProperty("messagesUsedToday").GetInt32(), body.GetProperty("messagesRemainingToday").GetInt32()).Should().Be((5, 2, 3));
        (body.GetProperty("messageMaxLength").GetInt32(), body.GetProperty("maxHistoryMessages").GetInt32()).Should().Be((2000, 10));
    }

    [Fact]
    public async Task GetStatus_StudentWithOpenExam_ReturnsExamInProgress()
    {
        var (_, unitId, _, _) = await ExamTestData.SeedExamUnitAsync(factory, 2);
        var (_, client) = await SignedInStudentAsync(factory);
        await ExamTestData.StartAsync(client, unitId);

        var body = await client.GetFromJsonAsync<JsonElement>(StatusRoute, CancellationToken);

        body.GetProperty("examInProgress").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task GetStatus_Anonymous_Returns401()
    {
        using var anonymous = AuthTestClient.Create(factory);

        using var response = await anonymous.GetAsync(StatusRoute, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
