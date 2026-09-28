using Core.Localization;
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
    private readonly ILocalizer _localizer = Substitute.For<ILocalizer>();
    private readonly GradeQuestionDraftHandler _handler;

    public GradeQuestionDraftHandlerTests()
    {
        _richTextSanitizer.Sanitize(Arg.Any<string?>()).Returns(x => x.Arg<string?>() ?? string.Empty);
        _handler = new GradeQuestionDraftHandler(_richTextSanitizer, _localizer);
    }

    [Fact]
    public async Task Handle_CorrectMcq_ReturnsCorrectWithMaxScore()
    {
        var result = await _handler.Handle(new GradeQuestionDraftQuery(McqFields(), Json("""{"optionId":"b"}""")), TestContext.Current.CancellationToken);

        result.Should().Be(new QuestionGradeResult(1m, 1m, "Correct", 1, null));
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

    [Fact]
    public async Task Handle_MultiPartial_ReturnsLocalizedFeedback()
    {
        _localizer.GetMessage("GRADE_FEEDBACK_CHOICE_TALLY", Arg.Any<string?>(), Arg.Any<Dictionary<string, object>?>()).Returns("tally");
        var draft = McqFields("<p>Vectors?</p>") with
        {
            Type = QuestionType.Multi,
            Body = Json("""{"options":[{"id":"a","text":"Force"},{"id":"b","text":"Velocity"},{"id":"c","text":"Mass"}]}"""),
            GradingSpec = Json("""{"correctOptionIds":["a","b"],"partialCredit":true}"""),
        };

        var result = await _handler.Handle(new GradeQuestionDraftQuery(draft, Json("""{"optionIds":["a"]}""")), TestContext.Current.CancellationToken);

        result.Should().Be(new QuestionGradeResult(0.5m, 0.5m, "Partial", 1, "tally"));
    }
}
