using Elmanhg.Application.Avatar.Shared;
using Elmanhg.Domain.Avatar;
using Elmanhg.Infrastructure.AiService;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.ContentRetrieval;
using Elmanhg.Tests.Integration.Exams;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Avatar.AvatarTestData;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Avatar;

[Collection(ContentRetrievalCollection.Name)]
public sealed class AvatarConversationLogEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PostMessage_FirstLessonMessage_ReturnsConversationIdAndPersistsExchange()
    {
        var lessonId = await ContentRetrievalTestData.SeedPublishedLessonAsync(factory, "<p>V = I R</p>", "<p>R = V / I</p>", [], CancellationToken);
        var (unitId, subjectId) = await ReadScopeAsync(lessonId);
        var (student, client) = await SignedInFreeStudentAsync(factory);

        using var response = await PostMessageAsync(client, new { entryPoint = "Lesson", lessonId, conversationId = (Guid?)null, message = "ما هو قانون أوم؟" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var conversationId = (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("conversationId").GetGuid();
        var conversation = (await ReadConversationAsync(factory, conversationId))!;
        (conversation.StudentId, conversation.EntryPoint, conversation.LessonId, conversation.SubjectId, conversation.UnitId).Should().Be((student.Id, AvatarEntryPoint.Lesson, (Guid?)lessonId, (Guid?)subjectId, (Guid?)unitId));
        conversation.Messages.Should().HaveCount(2);
        var assistant = conversation.Messages.Single(x => x.Role == AvatarMessageRole.Assistant);
        (assistant.Model, assistant.PromptVersion, assistant.CostUsd).Should().Be((FakeAiServiceClient.FakeModel, FakeAiServiceClient.FakePromptVersion, (decimal?)0m));
        AvatarMessageJson.ReadContext(assistant.Context!).Bundle.Lesson!.Id.Should().Be(lessonId);
    }

    [Fact]
    public async Task PostMessage_WithConversationId_AppendsToSameConversation()
    {
        var (_, client) = await SignedInFreeStudentAsync(factory);
        using var first = await PostMessageAsync(client, new { entryPoint = "Global", message = "كيف أذاكر؟" });
        var conversationId = (await first.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("conversationId").GetGuid();

        using var second = await PostMessageAsync(client, new { entryPoint = "Global", conversationId, message = "وماذا بعد؟" });

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("conversationId").GetGuid().Should().Be(conversationId);
        var conversation = (await ReadConversationAsync(factory, conversationId))!;
        conversation.Messages.Select(x => x.Position).Order().Should().Equal(0, 1, 2, 3);
        conversation.MessageCount.Should().Be(4);
    }

    [Fact]
    public async Task PostMessage_OtherStudentsConversation_Returns404AndRecordsNothing()
    {
        var (_, owner) = await SignedInFreeStudentAsync(factory);
        using var first = await PostMessageAsync(owner, new { entryPoint = "Global", message = "كيف أذاكر؟" });
        var conversationId = (await first.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("conversationId").GetGuid();
        var (other, otherClient) = await SignedInFreeStudentAsync(factory);

        using var response = await PostMessageAsync(otherClient, new { entryPoint = "Global", conversationId, message = "سؤال" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ExamTestData.ReadCodeAsync(response)).Should().Be("AVATAR_CONVERSATION_NOT_FOUND");
        (await ReadUsageAsync(factory, other.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task PostMessage_ConversationOfAnotherContext_Returns400ContextMismatch()
    {
        var (_, client) = await SignedInFreeStudentAsync(factory);
        using var first = await PostMessageAsync(client, new { entryPoint = "Global", message = "كيف أذاكر؟" });
        var conversationId = (await first.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("conversationId").GetGuid();

        using var response = await PostMessageAsync(client, new { entryPoint = "Lesson", lessonId = Guid.NewGuid(), conversationId, message = "سؤال" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ExamTestData.ReadCodeAsync(response)).Should().Be("AVATAR_CONVERSATION_CONTEXT_MISMATCH");
    }

    private async Task<(Guid UnitId, Guid SubjectId)> ReadScopeAsync(Guid lessonId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var unitId = await context.Lessons.AsNoTracking().Where(x => x.Id == lessonId).Select(x => x.UnitId).SingleAsync(CancellationToken).ConfigureAwait(false);
        var subjectId = await context.Units.AsNoTracking().Where(x => x.Id == unitId).Select(x => x.SubjectId).SingleAsync(CancellationToken).ConfigureAwait(false);
        return (unitId, subjectId);
    }
}
