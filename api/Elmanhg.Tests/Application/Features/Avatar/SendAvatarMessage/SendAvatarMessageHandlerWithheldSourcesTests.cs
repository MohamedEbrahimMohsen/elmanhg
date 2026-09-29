using Elmanhg.Application.Avatar.Shared;
using Elmanhg.Application.ContentRetrieval.Shared;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Domain.Avatar;
using Elmanhg.Domain.ContentRetrieval;
using Elmanhg.Domain.Sessions;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Avatar.SendAvatarMessage;

public sealed class SendAvatarMessageHandlerWithheldSourcesTests
{
    private readonly AvatarTestData.SendHarness _harness = new();

    [Fact]
    public async Task Handle_LessonEntryWithOpenQuiz_WithholdsUnansweredQuestionChunkFromSourcesAndCitations()
    {
        var quiz = _harness.Quiz(answered: true);
        var (explanation, answered, unanswered) = StubMatches(quiz);

        var result = await _harness.SendAsync(_harness.LessonCommand());

        _harness.LastChat!.Sources.Should().Equal(new[] { explanation, answered }.Select(AvatarSourceFactory.Create));
        result.Citations.Select(x => x.Reference).Should().Equal(explanation.Reference, answered.Reference);
        result.Citations.Should().NotContain(x => x.QuestionId == unanswered.QuestionId);
    }

    [Fact]
    public async Task Handle_QuizQuestionEntry_SendsOwnAnsweredChunkAndWithholdsOtherUnansweredItem()
    {
        var quiz = _harness.Quiz(answered: true);
        var (_, answered, unanswered) = StubMatches(quiz);

        await _harness.SendAsync(_harness.QuestionCommand(AvatarEntryPoint.QuizQuestion, quiz, quiz.Items[0].QuestionId));

        _harness.LastChat!.Sources.Select(x => x.Reference).Should().Contain(answered.Reference).And.NotContain(unanswered.Reference);
    }

    [Fact]
    public async Task Handle_SubmittedQuiz_SendsUnansweredQuestionChunk()
    {
        var quiz = _harness.Quiz(answered: true);
        quiz.Submit();
        var (explanation, answered, unanswered) = StubMatches(quiz);

        await _harness.SendAsync(_harness.LessonCommand());

        _harness.LastChat!.Sources.Should().Equal(new[] { explanation, answered, unanswered }.Select(AvatarSourceFactory.Create));
    }

    [Fact]
    public async Task Handle_OtherStudentsOpenQuiz_DoesNotWithhold()
    {
        var questions = _harness.RegisterQuestions(2);
        var other = Session.StartQuiz(Guid.NewGuid(), _harness.Lesson, questions, false);
        AvatarTestData.StubSessions(_harness.Sessions, other);
        var (explanation, first, second) = StubMatches(other);

        await _harness.SendAsync(_harness.LessonCommand());

        _harness.LastChat!.Sources.Should().Equal(new[] { explanation, first, second }.Select(AvatarSourceFactory.Create));
    }

    [Fact]
    public async Task Handle_AllMatchesWithheld_SendsLessonTextInstead()
    {
        var quiz = _harness.Quiz(answered: false);
        _harness.Lesson.Update(_harness.Lesson.Name, "<p>V = I R</p>", "<p>R = V / I</p>", null, [], Guid.NewGuid());
        AvatarTestData.StubSearch(_harness.Sender, QuestionMatch(quiz.Items[0].QuestionId, 1), QuestionMatch(quiz.Items[1].QuestionId, 2));

        var result = await _harness.SendAsync(_harness.LessonCommand());

        _harness.LastChat!.Sources.Should().BeEmpty();
        _harness.LastChat.Context.Lesson!.Explanation.Should().Be("V = I R");
        result.Citations.Should().BeEmpty();
    }

    private static LessonContentMatchResult QuestionMatch(Guid questionId, int position) => AvatarTestData.Match($"question-{questionId}-{position}", LessonContentSection.QuestionExplanation, null, questionId);

    private (LessonContentMatchResult Explanation, LessonContentMatchResult First, LessonContentMatchResult Second) StubMatches(Session session)
    {
        var explanation = AvatarTestData.Match("explanation-1", LessonContentSection.Explanation);
        var first = QuestionMatch(session.Items[0].QuestionId, 1);
        var second = QuestionMatch(session.Items[1].QuestionId, 1);
        AvatarTestData.StubSearch(_harness.Sender, explanation, first, second);
        _harness.Ai.ChatAsync(Arg.Any<AiChatRequest>(), Arg.Any<CancellationToken>()).Returns(new AiChatReply("رد", "m", "v2", 1, 1, "end_turn", [explanation.Reference, first.Reference, second.Reference]));
        return (explanation, first, second);
    }
}
