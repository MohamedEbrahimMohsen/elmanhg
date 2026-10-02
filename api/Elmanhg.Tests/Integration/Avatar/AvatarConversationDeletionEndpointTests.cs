using Elmanhg.Domain.Avatar;
using Elmanhg.Domain.Identity;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Configuration;
using Elmanhg.Tests.Integration.Exams;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Elmanhg.Tests.Integration.Avatar.AvatarTestData;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Avatar;

[Collection(RuntimeSettingsCollection.Name)]
public sealed class AvatarConversationDeletionEndpointTests(ApiFactory factory) : IAsyncLifetime
{
    private const string FlagKey = "features.studentsCanDeleteAvatarChats";
    private const string StatusRoute = $"{AvatarTestData.Route}/status";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await ConfigurationTestData.ClearOverridesAsync(factory);

    public async ValueTask DisposeAsync() => await ConfigurationTestData.ClearOverridesAsync(factory);

    [Fact]
    public async Task Delete_Own_ErasesMessagesAndTrainingRecords()
    {
        var (student, client) = await StudentAsync();
        var conversation = await SeedAsync(student.Id);

        using var response = await DeleteMyConversationAsync(client, conversation.Id);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tombstone = await ReadConversationIncludingDeletedAsync(factory, conversation.Id);
        (tombstone!.IsDeleted, tombstone.MessageCount).Should().Be((true, 0));
        tombstone.Messages.Should().BeEmpty();
        (await ReadTrainingRecordsAsync(factory, conversation.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_Own_HiddenFromStudentAndAdminViews()
    {
        var name = $"Hana {Guid.NewGuid():N}";
        var student = await AuthTestClient.SeedUserAsync(factory, User.CreateStudentWithEmail(name, AuthTestClient.NewEmail()), ScopeTestData.Password, suspended: false, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, student, CancellationToken);
        var conversation = await SeedAsync(student.Id);
        using var admin = await ConfigurationTestData.AdminClientAsync(factory);
        (await DeleteMyConversationAsync(client, conversation.Id)).Dispose();

        using var mine = await GetMyConversationsAsync(client, string.Empty);
        using var mineDetail = await GetMyConversationAsync(client, conversation.Id);
        using var adminList = await GetConversationsAsync(admin, $"?search={Uri.EscapeDataString(name)}");
        using var adminDetail = await GetConversationAsync(admin, conversation.Id);

        (await ItemIdsAsync(mine)).Should().NotContain(conversation.Id);
        mineDetail.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ItemIdsAsync(adminList)).Should().BeEmpty();
        adminDetail.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ExamTestData.ReadCodeAsync(adminDetail)).Should().Be("AVATAR_CONVERSATION_NOT_FOUND");
    }

    [Fact]
    public async Task Delete_AfterSending_KeepsDailyUsage()
    {
        var (student, client) = await StudentAsync();
        using var sent = await PostMessageAsync(client, new { entryPoint = "Global", history = Array.Empty<object>(), message = "كيف أذاكر الفيزياء؟" });
        var conversationId = (await sent.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("conversationId").GetGuid();

        using var response = await DeleteMyConversationAsync(client, conversationId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetFromJsonAsync<JsonElement>(StatusRoute, CancellationToken)).GetProperty("messagesUsedToday").GetInt32().Should().Be(1);
        (await ReadUsageAsync(factory, student.Id)).Should().ContainSingle();
    }

    [Fact]
    public async Task Delete_Own_AuditedWithMessageCountAndNoText()
    {
        var (student, client) = await StudentAsync();
        var question = $"secret question {Guid.NewGuid():N}";
        var reply = $"secret reply {Guid.NewGuid():N}";
        var conversation = await SeedAsync(student.Id, question, reply);

        (await DeleteMyConversationAsync(client, conversation.Id)).Dispose();

        var audit = (await ConfigurationTestData.ReadAuditsAsync(factory, "AvatarConversation.Delete", conversation.Id)).Should().ContainSingle().Subject;
        (audit.Outcome, audit.ActorUserId, audit.ResourceType).Should().Be(("Success", (Guid?)student.Id, "AvatarConversation"));
        var messageCount = JsonNode.Parse(audit.Diff!)![0]!["properties"]!["messageCount"]!;
        (messageCount["before"]!.GetValue<int>(), messageCount["after"]!.GetValue<int>()).Should().Be((2, 0));
        audit.Diff.Should().NotContain(question).And.NotContain(reply);
    }

    [Fact]
    public async Task Delete_OtherStudents_Returns404AndKeepsMessages()
    {
        var other = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var conversation = await SeedAsync(other.Id);
        var (_, client) = await StudentAsync();

        using var response = await DeleteMyConversationAsync(client, conversation.Id);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ExamTestData.ReadCodeAsync(response)).Should().Be("AVATAR_CONVERSATION_NOT_FOUND");
        (await ReadConversationAsync(factory, conversation.Id))!.Messages.Should().HaveCount(2);
    }

    [Fact]
    public async Task Delete_WhenSettingOff_Returns400AndKeepsMessages()
    {
        using var admin = await ConfigurationTestData.AdminClientAsync(factory);
        (await admin.PutAsJsonAsync(ConfigurationTestData.SettingRoute(FlagKey), new { value = false }, CancellationToken)).StatusCode.Should().Be(HttpStatusCode.OK);
        var (student, client) = await StudentAsync();
        var conversation = await SeedAsync(student.Id);

        using var response = await DeleteMyConversationAsync(client, conversation.Id);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ExamTestData.ReadCodeAsync(response)).Should().Be("AVATAR_CONVERSATION_DELETION_DISABLED");
        (await ReadConversationAsync(factory, conversation.Id))!.Messages.Should().HaveCount(2);
        (await client.GetFromJsonAsync<JsonElement>(StatusRoute, CancellationToken)).GetProperty("conversationDeletionEnabled").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task SendMessage_DeletedConversationId_Returns404()
    {
        var (student, client) = await StudentAsync();
        var conversation = await SeedAsync(student.Id);
        (await DeleteMyConversationAsync(client, conversation.Id)).Dispose();

        using var response = await PostMessageAsync(client, new { entryPoint = "Global", conversationId = conversation.Id, history = Array.Empty<object>(), message = "q" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ExamTestData.ReadCodeAsync(response)).Should().Be("AVATAR_CONVERSATION_NOT_FOUND");
    }

    [Fact]
    public async Task Delete_Anonymous_Returns401()
    {
        using var anonymous = AuthTestClient.Create(factory);

        using var response = await DeleteMyConversationAsync(anonymous, Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_Admin_Returns403()
    {
        using var admin = await ConfigurationTestData.AdminClientAsync(factory);

        using var response = await DeleteMyConversationAsync(admin, Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetConfigurationSettings_ChatDeletionFlag_ListedUnderFeaturesAndOn()
    {
        using var admin = await ConfigurationTestData.AdminClientAsync(factory);

        var groups = await admin.GetFromJsonAsync<JsonElement>(ConfigurationTestData.SettingsRoute, CancellationToken);

        var setting = groups.EnumerateArray().Single(x => x.GetProperty("group").GetString() == "Features").GetProperty("settings").EnumerateArray().Single(x => x.GetProperty("key").GetString() == FlagKey);
        (setting.GetProperty("value").GetBoolean(), setting.GetProperty("defaultValue").GetBoolean(), setting.GetProperty("isOverridden").GetBoolean()).Should().Be((true, true, false));
    }

    [Fact]
    public async Task GetStatus_Default_ReportsConversationDeletionEnabled()
    {
        var (_, client) = await StudentAsync();

        var body = await client.GetFromJsonAsync<JsonElement>(StatusRoute, CancellationToken);

        body.GetProperty("conversationDeletionEnabled").GetBoolean().Should().BeTrue();
    }

    private Task<(User Student, HttpClient Client)> StudentAsync() => SignedInFreeStudentAsync(factory);

    private async Task<AvatarConversation> SeedAsync(Guid studentId, string question = "question", string reply = "reply")
    {
        var conversation = new AvatarConversationBuilder().ForStudent(studentId).WithExchange(question, reply).Build();
        await SeedConversationAsync(factory, conversation).ConfigureAwait(false);
        return conversation;
    }

    private static async Task<List<Guid>> ItemIdsAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken).ConfigureAwait(false);
        return body.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList();
    }
}
