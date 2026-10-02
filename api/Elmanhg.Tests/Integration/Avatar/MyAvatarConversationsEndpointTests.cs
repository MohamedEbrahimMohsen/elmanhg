using Elmanhg.Domain.Avatar;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Exams;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Avatar.AvatarTestData;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Avatar;

public sealed class MyAvatarConversationsEndpointTests(ApiFactory factory)
{
    private static readonly DateTimeOffset StartedAt = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetMine_Student_ReturnsOwnConversationsNewestFirst()
    {
        var subjectName = $"Physics {Guid.NewGuid():N}";
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, subjectName, CancellationToken);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Electricity", 1, CancellationToken);
        var lessonId = await ContentTestData.SeedLessonAsync(factory, unitId, "Ohm's law", 1, [], CancellationToken);
        var (student, client) = await SignedInFreeStudentAsync(factory);
        var other = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var older = await SeedAsync(new AvatarConversationBuilder().ForStudent(student.Id).StartedAt(StartedAt).WithEntryPoint(AvatarEntryPoint.Lesson).WithLesson(subjectId, unitId, lessonId).WithExchange("What is current?", "r"));
        var newer = await SeedAsync(new AvatarConversationBuilder().ForStudent(student.Id).StartedAt(StartedAt.AddDays(1)).WithExchange("How do I study?", "r"));
        await SeedAsync(new AvatarConversationBuilder().ForStudent(other.Id).StartedAt(StartedAt.AddDays(2)).WithExchange("foreign", "r"));

        using var response = await GetMyConversationsAsync(client, "?pageNumber=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("items").EnumerateArray().ToList();
        items.Select(x => x.GetProperty("id").GetGuid()).Should().Equal(newer.Id, older.Id);
        items.Select(x => x.GetProperty("firstQuestion").GetString()).Should().Equal("How do I study?", "What is current?");
        (items[1].GetProperty("lessonName").GetString(), items[1].GetProperty("subjectName").GetString()).Should().Be(("Ohm's law", subjectName));
        items[1].GetProperty("messageCount").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task GetMine_PageSizeOverMax_Returns422()
    {
        var (_, client) = await SignedInFreeStudentAsync(factory);

        using var response = await GetMyConversationsAsync(client, "?pageSize=51");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ExamTestData.ReadCodeAsync(response)).Should().Be("AVATAR_CONVERSATIONS_PAGE_SIZE_INVALID");
    }

    [Fact]
    public async Task GetMine_ExamInProgress_Returns403()
    {
        var (_, unitId, _, _) = await ExamTestData.SeedExamUnitAsync(factory, 2);
        var (_, client) = await SignedInStudentAsync(factory);
        await ExamTestData.StartAsync(client, unitId);

        using var response = await GetMyConversationsAsync(client, string.Empty);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ExamTestData.ReadCodeAsync(response)).Should().Be("AVATAR_EXAM_IN_PROGRESS");
    }

    [Fact]
    public async Task GetMine_Anonymous_Returns401()
    {
        using var anonymous = AuthTestClient.Create(factory);

        using var response = await GetMyConversationsAsync(anonymous, string.Empty);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMine_Teacher_Returns403()
    {
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, CancellationToken);

        using var response = await GetMyConversationsAsync(client, string.Empty);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetMineDetail_Own_ReturnsMessagesWithoutReplyMetadata()
    {
        var (student, client) = await SignedInFreeStudentAsync(factory);
        var conversation = await SeedAsync(new AvatarConversationBuilder().ForStudent(student.Id).WithExchange("q1", "r1").WithExchange("q2", "r2"));

        using var response = await GetMyConversationAsync(client, conversation.Id);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var messages = (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("messages").EnumerateArray().ToList();
        messages.Select(x => x.GetProperty("text").GetString()).Should().Equal("q1", "r1", "q2", "r2");
        messages.Select(x => x.GetProperty("role").GetString()).Should().Equal("Student", "Assistant", "Student", "Assistant");
        messages.Should().AllSatisfy(x => x.TryGetProperty("citations", out _).Should().BeTrue());
        messages.Should().AllSatisfy(x => (x.TryGetProperty("model", out _) || x.TryGetProperty("costUsd", out _) || x.TryGetProperty("context", out _)).Should().BeFalse());
    }

    [Fact]
    public async Task GetMineDetail_OtherStudents_Returns404()
    {
        var other = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var conversation = await SeedAsync(new AvatarConversationBuilder().ForStudent(other.Id).WithExchange("q", "r"));
        var (_, client) = await SignedInFreeStudentAsync(factory);

        using var response = await GetMyConversationAsync(client, conversation.Id);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ExamTestData.ReadCodeAsync(response)).Should().Be("AVATAR_CONVERSATION_NOT_FOUND");
    }

    private async Task<AvatarConversation> SeedAsync(AvatarConversationBuilder builder)
    {
        var conversation = builder.Build();
        await SeedConversationAsync(factory, conversation).ConfigureAwait(false);
        return conversation;
    }
}
