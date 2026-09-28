using Elmanhg.Application.Questions.GradeQuestionDraft;
using Elmanhg.Application.Shared.RichText;
using Elmanhg.Domain.Questions;
using FluentAssertions;
using NSubstitute;
using static Elmanhg.Tests.Builders.QuestionBuilder;

namespace Elmanhg.Tests.Application.Features.Questions.GradeQuestionDraft;

public sealed class GradeQuestionDraftHandlerTests
{
    private readonly IRichTextSanitizer _richTextSanitizer = Substitute.For<IRichTextSanitizer>();
    private readonly GradeQuestionDraftHandler _handler;

    public GradeQuestionDraftHandlerTests()
    {
        _richTextSanitizer.Sanitize(Arg.Any<string?>()).Returns(x => x.Arg<string?>() ?? string.Empty);
        _handler = new GradeQuestionDraftHandler(_richTextSanitizer);
    }

    [Fact]
    public async Task Handle_CorrectMcq_ReturnsCorrectWithMaxScore()
    {
        var result = await _handler.Handle(new GradeQuestionDraftQuery(McqFields(), Json("""{"optionId":"b"}""")), TestContext.Current.CancellationToken);

        result.Should().Be(new QuestionGradeResult(1m, 1m, "Correct", 1));
    }

    [Fact]
    public async Task Handle_FillOneOfTwo_ReturnsPartial()
    {
        var draft = McqFields("<p>[[1]] m and [[2]] s</p>") with
        {
            Type = QuestionType.Fill,
            Body = Json("""{"blanks":[{"id":"1"},{"id":"2"}]}"""),
            GradingSpec = Json("""{"blanks":[{"id":"1","acceptedAnswers":["20"]},{"id":"2","acceptedAnswers":["5"]}]}"""),
            MaxScore = 2,
        };

        var result = await _handler.Handle(new GradeQuestionDraftQuery(draft, Json("""{"blanks":[{"id":"1","text":"20"},{"id":"2","text":"6"}]}""")), TestContext.Current.CancellationToken);

        result.Score.Should().Be(1m);
        result.Outcome.Should().Be("Partial");
    }
}
