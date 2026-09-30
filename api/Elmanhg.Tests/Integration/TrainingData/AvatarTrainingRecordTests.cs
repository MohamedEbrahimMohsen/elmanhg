using Elmanhg.Domain.Avatar;
using Elmanhg.Tests.Integration.ContentRetrieval;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Avatar.AvatarTestData;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;
using static Elmanhg.Tests.Integration.TrainingData.TrainingDataTestData;

namespace Elmanhg.Tests.Integration.TrainingData;

[Collection(ContentRetrievalCollection.Name)]
public sealed class AvatarTrainingRecordTests(ApiFactory factory)
{
    private const string Message = "ما هو قانون أوم؟";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PostMessage_Reply_WritesRecordWithTextsModelAndContext()
    {
        var lessonId = await ContentRetrievalTestData.SeedPublishedLessonAsync(factory, "<p>V = I R</p>", "<p>R = V / I</p>", [], CancellationToken);
        var (student, client) = await SignedInFreeStudentAsync(factory);

        using var response = await PostMessageAsync(client, new { entryPoint = "Lesson", lessonId, message = Message });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        var conversation = (await ReadConversationAsync(factory, body.GetProperty("conversationId").GetGuid()))!;
        var assistant = conversation.Messages.Single(x => x.Role == AvatarMessageRole.Assistant);
        var record = (await ReadAvatarRecordsAsync(factory, ExpectedHash(student.Id))).Should().ContainSingle().Subject;
        (record.StudentText, record.AssistantText).Should().Be((Message, assistant.Text));
        (record.Model, record.PromptVersion).Should().Be((assistant.Model!, assistant.PromptVersion!));
        (record.ConversationId, record.AssistantMessageId, record.EntryPoint, record.LessonId).Should().Be((conversation.Id, assistant.Id, AvatarEntryPoint.Lesson, (Guid?)lessonId));
        record.Context.Should().Be(assistant.Context);
    }

    [Fact]
    public async Task PostMessage_SecondMessage_WritesSecondRecordAtPositionTwo()
    {
        var (student, client) = await SignedInFreeStudentAsync(factory);
        using var first = await PostMessageAsync(client, new { entryPoint = "Global", message = "كيف أذاكر؟" });
        var conversationId = (await first.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("conversationId").GetGuid();

        using var second = await PostMessageAsync(client, new { entryPoint = "Global", conversationId, message = "وماذا بعد؟" });

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var records = await ReadAvatarRecordsAsync(factory, ExpectedHash(student.Id));
        records.Should().HaveCount(2).And.OnlyContain(x => x.ConversationId == conversationId);
        records.Select(x => x.StudentMessagePosition).Order().Should().Equal(0, 2);
    }

    [Fact]
    public async Task PostMessage_BlankMessage_Returns422AndWritesNoRecord()
    {
        var (student, client) = await SignedInFreeStudentAsync(factory);

        using var response = await PostMessageAsync(client, new { entryPoint = "Global", message = "   " });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadAvatarRecordsAsync(factory, ExpectedHash(student.Id))).Should().BeEmpty();
    }
}
