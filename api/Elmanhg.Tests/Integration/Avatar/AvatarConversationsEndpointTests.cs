using Elmanhg.Domain.Avatar;
using Elmanhg.Domain.Identity;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Exams;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.Payments;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Avatar.AvatarTestData;

namespace Elmanhg.Tests.Integration.Avatar;

public sealed class AvatarConversationsEndpointTests(ApiFactory factory)
{
    private static readonly DateTimeOffset StartedAt = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetConversations_Admin_ReturnsMatchesNewestFirstWithNames()
    {
        var token = Token();
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var older = await SeedAsync(new AvatarConversationBuilder().ForStudent(student.Id).StartedAt(StartedAt).WithExchange($"older {token}", "r"));
        var newer = await SeedAsync(new AvatarConversationBuilder().ForStudent(student.Id).StartedAt(StartedAt.AddDays(1)).WithExchange("newer", $"reply {token}"));
        using var admin = await PaymentsTestData.AdminClientAsync(factory, CancellationToken);

        var items = await ItemsAsync(admin, $"?search={token}");

        items.Select(x => x.GetProperty("id").GetGuid()).Should().Equal(newer.Id, older.Id);
        items.Select(x => x.GetProperty("studentName").GetString()).Should().AllBe("Student");
        items.Select(x => x.GetProperty("firstQuestion").GetString()).Should().Equal("newer", $"older {token}");
    }

    [Fact]
    public async Task GetConversations_SearchStudentName_ReturnsTheirConversations()
    {
        var name = $"Sara {Token()}";
        var student = await AuthTestClient.SeedUserAsync(factory, User.CreateStudentWithEmail(name, AuthTestClient.NewEmail()), ScopeTestData.Password, suspended: false, CancellationToken);
        var other = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var theirs = await SeedAsync(new AvatarConversationBuilder().ForStudent(student.Id).WithExchange("q", "r"));
        await SeedAsync(new AvatarConversationBuilder().ForStudent(other.Id).WithExchange("q", "r"));
        using var admin = await PaymentsTestData.AdminClientAsync(factory, CancellationToken);

        var items = await ItemsAsync(admin, $"?search={Uri.EscapeDataString(name.ToUpperInvariant())}");

        items.Should().ContainSingle().Which.GetProperty("id").GetGuid().Should().Be(theirs.Id);
    }

    [Fact]
    public async Task GetConversations_EntryPointFilter_ExcludesOtherEntryPoints()
    {
        var token = Token();
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var global = await SeedAsync(new AvatarConversationBuilder().ForStudent(student.Id).WithExchange(token, "r"));
        await SeedAsync(new AvatarConversationBuilder().ForStudent(student.Id).WithEntryPoint(AvatarEntryPoint.QuizQuestion).WithExchange(token, "r"));
        using var admin = await PaymentsTestData.AdminClientAsync(factory, CancellationToken);

        var items = await ItemsAsync(admin, $"?search={token}&entryPoint=Global");

        items.Should().ContainSingle().Which.GetProperty("id").GetGuid().Should().Be(global.Id);
    }

    [Fact]
    public async Task GetConversations_PageSizeOverMax_Returns422()
    {
        using var admin = await PaymentsTestData.AdminClientAsync(factory, CancellationToken);

        using var response = await GetConversationsAsync(admin, "?pageSize=101");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ExamTestData.ReadCodeAsync(response)).Should().Be("AVATAR_CONVERSATIONS_PAGE_SIZE_INVALID");
    }

    [Fact]
    public async Task GetConversations_Student_Returns403()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, student, CancellationToken);

        using var response = await GetConversationsAsync(client, string.Empty);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetConversations_Anonymous_Returns401()
    {
        using var anonymous = AuthTestClient.Create(factory);

        using var response = await GetConversationsAsync(anonymous, string.Empty);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetConversation_Admin_ReturnsMessagesWithReplyMetadata()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var conversation = await SeedAsync(new AvatarConversationBuilder().ForStudent(student.Id).WithExchange("q", "r"));
        using var admin = await PaymentsTestData.AdminClientAsync(factory, CancellationToken);

        using var response = await GetConversationAsync(admin, conversation.Id);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var messages = (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("messages");
        messages[0].GetProperty("role").GetString().Should().Be("Student");
        messages[1].GetProperty("model").GetString().Should().Be("claude-sonnet-5");
        messages[1].GetProperty("costUsd").GetDecimal().Should().Be(0.0021m);
        messages[1].GetProperty("context").GetProperty("bundle").GetProperty("entryPoint").GetString().Should().Be("Global");
    }

    [Fact]
    public async Task GetConversation_Unknown_Returns404()
    {
        using var admin = await PaymentsTestData.AdminClientAsync(factory, CancellationToken);

        using var response = await GetConversationAsync(admin, Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ExamTestData.ReadCodeAsync(response)).Should().Be("AVATAR_CONVERSATION_NOT_FOUND");
    }

    [Fact]
    public async Task GetConversation_Teacher_Returns403()
    {
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, CancellationToken);

        using var response = await GetConversationAsync(client, Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static string Token() => $"tok{Guid.NewGuid():N}";

    private async Task<AvatarConversation> SeedAsync(AvatarConversationBuilder builder)
    {
        var conversation = builder.Build();
        await SeedConversationAsync(factory, conversation).ConfigureAwait(false);
        return conversation;
    }

    private static async Task<List<JsonElement>> ItemsAsync(HttpClient client, string query)
    {
        using var response = await GetConversationsAsync(client, query).ConfigureAwait(false);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken).ConfigureAwait(false);
        return body.GetProperty("items").EnumerateArray().ToList();
    }
}
