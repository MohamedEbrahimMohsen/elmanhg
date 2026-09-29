using Core.Errors;
using Elmanhg.Application.Avatar.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Domain.Avatar;
using Elmanhg.Domain.ContentRetrieval;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Elmanhg.Tests.Application.Features.Avatar.SendAvatarMessage;

public sealed class SendAvatarMessageHandlerConversationTests
{
    private readonly AvatarTestData.SendHarness _harness = new();

    [Fact]
    public async Task Handle_NoConversationId_StartsConversationWithExchangeAndReturnsItsId()
    {
        var result = await _harness.SendAsync(_harness.LessonCommand());

        await _harness.Conversations.Received(1).AddAsync(Arg.Any<AvatarConversation>(), Arg.Any<CancellationToken>());
        _harness.AddedConversation!.Messages.Should().HaveCount(2);
        result.ConversationId.Should().Be(_harness.AddedConversation.Id);
        await _harness.Usage.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NewLessonConversation_RecordsSubjectUnitLessonIds()
    {
        await _harness.SendAsync(_harness.LessonCommand());

        var conversation = _harness.AddedConversation!;
        (conversation.SubjectId, conversation.UnitId, conversation.LessonId).Should().Be(((Guid?)_harness.Builder.Questions.Subject.Id, (Guid?)_harness.Builder.Questions.Unit.Id, (Guid?)_harness.Lesson.Id));
        (conversation.SessionId, conversation.QuestionId).Should().Be(((Guid?)null, (Guid?)null));
    }

    [Fact]
    public async Task Handle_NewQuizQuestionConversation_RecordsSessionAndQuestionIds()
    {
        var quiz = _harness.Quiz(answered: true);
        var questionId = quiz.Items[0].QuestionId;

        await _harness.SendAsync(_harness.QuestionCommand(AvatarEntryPoint.QuizQuestion, quiz, questionId));

        var conversation = _harness.AddedConversation!;
        (conversation.EntryPoint, conversation.SessionId, conversation.QuestionId, conversation.LessonId).Should().Be((AvatarEntryPoint.QuizQuestion, (Guid?)quiz.Id, (Guid?)questionId, (Guid?)_harness.Lesson.Id));
    }

    [Fact]
    public async Task Handle_Reply_StoresModelPromptTokensCostAndStopReason()
    {
        await _harness.SendAsync(_harness.LessonCommand());

        var assistant = _harness.AddedConversation!.Messages[1];
        (assistant.Model, assistant.PromptVersion, assistant.InputTokens, assistant.OutputTokens).Should().Be(("claude-sonnet-5", "v2", (int?)10, (int?)5));
        (assistant.CostUsd, assistant.StopReason, assistant.HistoryMessageCount).Should().Be(((decimal?)0.0021m, "end_turn", (int?)0));
    }

    [Fact]
    public async Task Handle_Reply_StoresContextBundleAndSourcesJson()
    {
        var match = AvatarTestData.Match("explanation-1", LessonContentSection.Explanation, "قانون أوم");
        AvatarTestData.StubSearch(_harness.Sender, match);

        await _harness.SendAsync(_harness.LessonCommand());

        var context = AvatarMessageJson.ReadContext(_harness.AddedConversation!.Messages[1].Context!);
        context.Bundle.Lesson!.Id.Should().Be(_harness.Lesson.Id);
        context.Sources.Should().Equal(_harness.LastChat!.Sources);
        context.Sources.Should().Equal(AvatarSourceFactory.Create(match));
    }

    [Fact]
    public async Task Handle_Reply_StoresMappedCitationsJson()
    {
        AvatarTestData.StubSearch(_harness.Sender, AvatarTestData.Match("explanation-1", LessonContentSection.Explanation, "قانون أوم"));
        _harness.Ai.ChatAsync(Arg.Any<AiChatRequest>(), Arg.Any<CancellationToken>()).Returns(new AiChatReply("رد", "m", "v2", 1, 1, "end_turn", ["explanation-1"], 0m));

        var result = await _harness.SendAsync(_harness.LessonCommand());

        result.Citations.Should().NotBeEmpty();
        AvatarMessageJson.ReadCitations(_harness.AddedConversation!.Messages[1].Citations!).Should().Equal(result.Citations);
    }

    [Fact]
    public async Task Handle_ExistingConversation_SendsRecentStoredMessagesAsHistory()
    {
        var conversation = Existing();
        _harness.AvatarOptions.MaxHistoryMessages = 4;

        await _harness.SendAsync(_harness.LessonCommand() with { ConversationId = conversation.Id });

        _harness.LastChat!.History.Should().Equal(new AiChatMessage(AiChatRole.User, "q2"), new AiChatMessage(AiChatRole.Assistant, "r2"), new AiChatMessage(AiChatRole.User, "q3"), new AiChatMessage(AiChatRole.Assistant, "r3"));
        conversation.Messages[^1].HistoryMessageCount.Should().Be(4);
    }

    [Fact]
    public async Task Handle_ExistingConversation_AppendsAtNextPositionsWithoutAdding()
    {
        var conversation = Existing();

        var result = await _harness.SendAsync(_harness.LessonCommand() with { ConversationId = conversation.Id });

        conversation.Messages.Skip(6).Select(x => x.Position).Should().Equal(6, 7);
        await _harness.Conversations.DidNotReceive().AddAsync(Arg.Any<AvatarConversation>(), Arg.Any<CancellationToken>());
        result.ConversationId.Should().Be(conversation.Id);
    }

    [Fact]
    public async Task Handle_StoredTurnOverHistoryTurnMax_IsTruncated()
    {
        var conversation = Existing("question ", "answer ");
        _harness.AvatarOptions.HistoryTurnMaxLength = 5;

        await _harness.SendAsync(_harness.LessonCommand() with { ConversationId = conversation.Id });

        _harness.LastChat!.History.Should().NotBeEmpty();
        _harness.LastChat.History.Should().OnlyContain(x => x.Content.Length == 5);
    }

    [Fact]
    public async Task Handle_UnknownOrForeignConversation_ThrowsAvatarConversationNotFound()
    {
        var foreign = new AvatarConversationBuilder().WithEntryPoint(AvatarEntryPoint.Lesson).WithLesson(null, null, _harness.Lesson.Id).WithExchange("q1", "r1").Build();
        AvatarTestData.StubConversations(_harness.Conversations, foreign);

        var act = () => _harness.SendAsync(_harness.LessonCommand() with { ConversationId = foreign.Id });

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.AvatarConversationNotFound);
        await _harness.Ai.DidNotReceive().ChatAsync(Arg.Any<AiChatRequest>(), Arg.Any<CancellationToken>());
        await _harness.Usage.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ConversationOfAnotherContext_ThrowsAvatarConversationContextMismatch()
    {
        var global = new AvatarConversationBuilder().ForStudent(_harness.Builder.StudentId).WithExchange("q1", "r1").Build();
        AvatarTestData.StubConversations(_harness.Conversations, global);

        var act = () => _harness.SendAsync(_harness.LessonCommand() with { ConversationId = global.Id });

        (await act.Should().ThrowAsync<BadRequestCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.AvatarConversationContextMismatch);
        await _harness.Ai.DidNotReceive().ChatAsync(Arg.Any<AiChatRequest>(), Arg.Any<CancellationToken>());
        await _harness.Usage.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AiServiceUnavailable_StartsNoConversation()
    {
        _harness.Ai.ChatAsync(Arg.Any<AiChatRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(new ServiceUnavailableCoreException(ErrorCodes.AiServiceUnavailable));

        var act = () => _harness.SendAsync(_harness.LessonCommand());

        await act.Should().ThrowAsync<ServiceUnavailableCoreException>();
        await _harness.Conversations.DidNotReceive().AddAsync(Arg.Any<AvatarConversation>(), Arg.Any<CancellationToken>());
        await _harness.Usage.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private AvatarConversation Existing(string questionPrefix = "q", string replyPrefix = "r")
    {
        var builder = new AvatarConversationBuilder()
            .ForStudent(_harness.Builder.StudentId)
            .WithEntryPoint(AvatarEntryPoint.Lesson)
            .WithLesson(_harness.Builder.Questions.Subject.Id, _harness.Builder.Questions.Unit.Id, _harness.Lesson.Id);
        foreach (var number in Enumerable.Range(1, 3))
        {
            builder.WithExchange(questionPrefix + number, replyPrefix + number);
        }

        var conversation = builder.Build();
        AvatarTestData.StubConversations(_harness.Conversations, conversation);
        return conversation;
    }
}
