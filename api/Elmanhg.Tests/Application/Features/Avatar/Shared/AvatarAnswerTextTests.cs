using Elmanhg.Application.Avatar.Shared;
using Elmanhg.Domain.Questions;
using Elmanhg.Infrastructure.RichText;
using FluentAssertions;
using System.Text.Json.Nodes;

namespace Elmanhg.Tests.Application.Features.Avatar.Shared;

public sealed class AvatarAnswerTextTests
{
    private const string ChoiceBody = """{"options":[{"id":"a","text":"<p>a</p>"},{"id":"b","text":"<p>b</p>"},{"id":"c","text":"<p>c</p>"}]}""";
    private const string FillBody = """{"blanks":[{"id":"1"},{"id":"2"}]}""";

    private readonly RichTextExtractor _extractor = new();

    [Fact]
    public void StudentAnswer_Mcq_ReturnsOptionPlainText()
    {
        var snapshot = Snapshot(QuestionType.Mcq, """{"options":[{"id":"a","text":"<p>3</p>"},{"id":"b","text":"<p>4</p>"}]}""", """{"correctOptionId":"b"}""");

        AvatarAnswerText.StudentAnswer(snapshot, """{"optionId":"a"}""", _extractor).Should().Be("3");
    }

    [Fact]
    public void StudentAnswer_Multi_JoinsOptionsInBodyOrder()
    {
        var snapshot = Snapshot(QuestionType.Multi, ChoiceBody, """{"correctOptionIds":["a"]}""");

        AvatarAnswerText.StudentAnswer(snapshot, """{"optionIds":["c","a"]}""", _extractor).Should().Be("a، c");
    }

    [Fact]
    public void StudentAnswer_TrueFalse_ReturnsArabicWord()
    {
        var snapshot = Snapshot(QuestionType.TrueFalse, "{}", """{"correctAnswer":true}""");

        AvatarAnswerText.StudentAnswer(snapshot, """{"value":true}""", _extractor).Should().Be("صح");
        AvatarAnswerText.StudentAnswer(snapshot, """{"value":false}""", _extractor).Should().Be("خطأ");
    }

    [Fact]
    public void StudentAnswer_Fill_ListsBlanks()
    {
        var snapshot = Snapshot(QuestionType.Fill, FillBody, """{"blanks":[{"id":"1","acceptedAnswers":["5"]},{"id":"2","acceptedAnswers":["7"]}]}""");

        AvatarAnswerText.StudentAnswer(snapshot, """{"blanks":[{"id":"1","text":"5"},{"id":"2","text":"7"}]}""", _extractor).Should().Be("[[1]] 5، [[2]] 7");
    }

    [Fact]
    public void StudentAnswer_Null_ReturnsNull()
    {
        var snapshot = Snapshot(QuestionType.Mcq, ChoiceBody, """{"correctOptionId":"b"}""");

        AvatarAnswerText.StudentAnswer(snapshot, null, _extractor).Should().BeNull();
    }

    [Fact]
    public void CorrectAnswer_Mcq_ReturnsCorrectOptionText()
    {
        var snapshot = Snapshot(QuestionType.Mcq, ChoiceBody, """{"correctOptionId":"b"}""");

        AvatarAnswerText.CorrectAnswer(snapshot, _extractor).Should().Be("b");
    }

    [Fact]
    public void CorrectAnswer_ShortNumericAbsoluteTolerance_FormatsPlusMinus()
    {
        var snapshot = Snapshot(QuestionType.Short, """{"answerKind":"numeric"}""", """{"value":9.8,"tolerance":0.1,"toleranceMode":"absolute"}""");

        AvatarAnswerText.CorrectAnswer(snapshot, _extractor).Should().Be("9.8 ± 0.1");
    }

    [Fact]
    public void CorrectAnswer_ShortNumericPercentTolerance_AppendsPercent()
    {
        var snapshot = Snapshot(QuestionType.Short, """{"answerKind":"numeric"}""", """{"value":9.8,"tolerance":5,"toleranceMode":"percent"}""");

        AvatarAnswerText.CorrectAnswer(snapshot, _extractor).Should().Be("9.8 ± 5%");
    }

    [Fact]
    public void CorrectAnswer_ShortText_ReturnsFirstAcceptedAnswer()
    {
        var snapshot = Snapshot(QuestionType.Short, """{"answerKind":"text"}""", """{"acceptedAnswers":["الأوم","أوم"]}""");

        AvatarAnswerText.CorrectAnswer(snapshot, _extractor).Should().Be("الأوم");
    }

    [Fact]
    public void CorrectAnswer_FillBlanks_ListsFirstAcceptedAnswers()
    {
        var snapshot = Snapshot(QuestionType.Fill, FillBody, """{"blanks":[{"id":"1","acceptedAnswers":["5","خمسة"]},{"id":"2","acceptedAnswers":["7"]}]}""");

        AvatarAnswerText.CorrectAnswer(snapshot, _extractor).Should().Be("[[1]] 5، [[2]] 7");
    }

    [Fact]
    public void StudentAnswer_MathSteps_ListsStepsAndFinalAnswer()
    {
        var snapshot = Snapshot(QuestionType.MathSteps, "{}", """{"acceptedAnswers":["x = 2"],"form":"equivalent"}""");

        AvatarAnswerText.StudentAnswer(snapshot, """{"steps":["2x = 4"],"finalAnswer":"x = 2"}""", _extractor).Should().Be("2x = 4\nالإجابة النهائية: x = 2");
    }

    [Fact]
    public void CorrectAnswer_MathSteps_ReturnsFirstAcceptedAnswer()
    {
        var snapshot = Snapshot(QuestionType.MathSteps, "{}", """{"acceptedAnswers":["x = 2","2"],"form":"equivalent"}""");

        AvatarAnswerText.CorrectAnswer(snapshot, _extractor).Should().Be("x = 2");
    }

    private static QuestionRevisionSnapshot Snapshot(QuestionType type, string body, string spec) => new(type, "<p>stem</p>", JsonNode.Parse(body), JsonNode.Parse(spec), "<p>explanation</p>", 1);
}
