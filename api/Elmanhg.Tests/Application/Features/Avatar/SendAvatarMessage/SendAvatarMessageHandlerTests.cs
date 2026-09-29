using Core.Errors;
using Elmanhg.Application.Avatar.SendAvatarMessage;
using Elmanhg.Application.Avatar.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Domain.Avatar;
using Elmanhg.Domain.ContentRetrieval;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Elmanhg.Tests.Application.Features.Avatar.SendAvatarMessage;

public sealed class SendAvatarMessageHandlerTests
{
    private readonly AvatarTestData.SendHarness _harness = new();

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _harness.CurrentUser.UserId.Returns((Guid?)null);

        var act = () => _harness.SendAsync(_harness.LessonCommand());

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _harness.Usage.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExamInProgress_ThrowsAvatarExamInProgressWithoutCallingAi()
    {
        var exam = _harness.Builder.Build(timeLimitMinutes: null);
        AvatarTestData.StubSessions(_harness.Sessions, exam);

        var act = () => _harness.SendAsync(_harness.LessonCommand());

        (await act.Should().ThrowAsync<ForbiddenCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.AvatarExamInProgress);
        await _harness.Ai.DidNotReceive().ChatAsync(Arg.Any<AiChatRequest>(), Arg.Any<CancellationToken>());
        await _harness.Usage.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FreeStudentAtDailyLimit_ThrowsAvatarDailyLimitReachedWithLimit()
    {
        AvatarTestData.StubUsedToday(_harness.Usage, 5);

        var act = () => _harness.SendAsync(_harness.LessonCommand());

        var exception = (await act.Should().ThrowAsync<ForbiddenCoreException>()).Which;
        exception.ErrorCode.Should().Be(ErrorCodes.AvatarDailyLimitReached);
        exception.Context!["limit"].Should().Be(5);
        await _harness.Ai.DidNotReceive().ChatAsync(Arg.Any<AiChatRequest>(), Arg.Any<CancellationToken>());
        await _harness.Usage.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_LessonEntryWithMatches_SendsSourcesAndEmptiesLessonText()
    {
        _harness.Lesson.Update(_harness.Lesson.Name, "<p>V = I R</p>", "<p>R = V / I</p>", null, [], Guid.NewGuid());
        var matches = new[] { AvatarTestData.Match("explanation-1", LessonContentSection.Explanation, "قانون أوم"), AvatarTestData.Match("summary-1", LessonContentSection.Summary) };
        AvatarTestData.StubSearch(_harness.Sender, matches);

        await _harness.SendAsync(_harness.LessonCommand());

        var request = _harness.LastChat!;
        request.Context.EntryPoint.Should().Be(AiChatEntryPoint.Lesson);
        request.Context.Lesson!.Name.Should().Be(_harness.Lesson.Name);
        request.Context.Lesson.Explanation.Should().BeEmpty();
        request.Context.Lesson.Summary.Should().BeEmpty();
        request.Sources.Should().Equal(matches.Select(AvatarSourceFactory.Create));
    }

    [Fact]
    public async Task Handle_Success_RecordsUsageAndReturnsCountsAndCitations()
    {
        AvatarTestData.StubUsedToday(_harness.Usage, 2);
        AvatarTestData.StubSearch(_harness.Sender, AvatarTestData.Match("explanation-1", LessonContentSection.Explanation, "قانون أوم"));
        _harness.Ai.ChatAsync(Arg.Any<AiChatRequest>(), Arg.Any<CancellationToken>()).Returns(new AiChatReply("رد", "claude-sonnet-5", "v2", 10, 5, "end_turn", ["explanation-1"], 0.0021m));

        var result = await _harness.SendAsync(_harness.LessonCommand());

        await _harness.Usage.Received(1).AddAsync(Arg.Is<AvatarMessageUsage>(x => x.StudentId == _harness.Builder.StudentId && x.EntryPoint == AvatarEntryPoint.Lesson && x.CreatedAt == AvatarTestData.SendHarness.Now), Arg.Any<CancellationToken>());
        await _harness.Usage.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        result.Reply.Should().Be("رد");
        result.DailyMessageLimit.Should().Be(5);
        result.MessagesUsedToday.Should().Be(3);
        result.MessagesRemainingToday.Should().Be(2);
        result.Model.Should().Be("claude-sonnet-5");
        result.PromptVersion.Should().Be("v2");
        result.Citations.Should().Equal(new AvatarCitationResult("explanation-1", LessonContentSection.Explanation, "قانون أوم", _harness.Lesson.Id, null));
        result.ConversationId.Should().Be(_harness.AddedConversation!.Id);
    }

    [Fact]
    public async Task Handle_ReplyCitesUnknownReference_DropsIt()
    {
        AvatarTestData.StubSearch(_harness.Sender, AvatarTestData.Match("explanation-1", LessonContentSection.Explanation));
        _harness.Ai.ChatAsync(Arg.Any<AiChatRequest>(), Arg.Any<CancellationToken>()).Returns(new AiChatReply("رد", "m", "v2", 1, 1, "end_turn", ["summary-9", "explanation-1"], 0m));

        var result = await _harness.SendAsync(_harness.LessonCommand());

        result.Citations.Select(x => x.Reference).Should().Equal("explanation-1");
    }

    [Fact]
    public async Task Handle_AiServiceUnavailable_PropagatesAndRecordsNothing()
    {
        _harness.Ai.ChatAsync(Arg.Any<AiChatRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(new ServiceUnavailableCoreException(ErrorCodes.AiServiceUnavailable));

        var act = () => _harness.SendAsync(_harness.LessonCommand());

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.AiServiceUnavailable);
        await _harness.Usage.DidNotReceive().AddAsync(Arg.Any<AvatarMessageUsage>(), Arg.Any<CancellationToken>());
        await _harness.Usage.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
