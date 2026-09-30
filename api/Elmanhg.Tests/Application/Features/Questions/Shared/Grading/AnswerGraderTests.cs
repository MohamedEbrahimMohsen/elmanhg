using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.Shared.Grading;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Questions.Schemas;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using static Elmanhg.Tests.Builders.QuestionBuilder;

namespace Elmanhg.Tests.Application.Features.Questions.Shared.Grading;

public sealed class AnswerGraderTests
{
    private const string ToleranceSpec = """{"acceptedAnswers":["x = 2"],"form":"equivalent","tolerance":0.01,"toleranceMode":"absolute"}""";
    private readonly IAiMathCheckClient _mathCheckClient = Substitute.For<IAiMathCheckClient>();

    [Fact]
    public async Task GradeAsync_NonMathType_UsesDeterministicGraderWithoutClient()
    {
        var grade = await AnswerGrader.GradeAsync(QuestionType.Mcq, """{"correctOptionId":"b"}""", 1, Json("""{"optionId":"b"}"""), _mathCheckClient, TestContext.Current.CancellationToken);

        grade.Outcome.Should().Be(GradeOutcome.Correct);
        await _mathCheckClient.DidNotReceive().CheckAsync(Arg.Any<AiMathCheckRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GradeAsync_BlankFinalAnswer_ReturnsUnansweredWithoutClient()
    {
        var grade = await Grade(MathStepsSpecJson, """{"steps":["x"],"finalAnswer":"  "}""");

        (grade.NormalisedScore, grade.Feedback).Should().Be((0m, GradeFeedback.Unanswered));
        await _mathCheckClient.DidNotReceive().CheckAsync(Arg.Any<AiMathCheckRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GradeAsync_MathAnswer_SendsTrimmedFinalAnswerAndRules()
    {
        Reply(MathAnswerVerdict.Equivalent);

        var grade = await Grade(ToleranceSpec, """{"steps":["2x = 4"],"finalAnswer":"  x = 2 "}""");

        await _mathCheckClient.Received(1).CheckAsync(Arg.Is<AiMathCheckRequest>(x => x.Answer == "x = 2" && x.Expected.SequenceEqual(new[] { "x = 2" }) && x.Form == MathAnswerForm.Equivalent && x.Tolerance == 0.01m && x.ToleranceMode == ToleranceMode.Absolute), Arg.Any<CancellationToken>());
        (grade.Outcome, grade.Score).Should().Be((GradeOutcome.Correct, 2m));
    }

    [Fact]
    public async Task GradeAsync_WrongFormVerdict_ReturnsZeroWithWrongFormFeedback()
    {
        Reply(MathAnswerVerdict.WrongForm);

        var grade = await Grade(MathStepsSpecJson, """{"finalAnswer":"x = 4/2"}""");

        (grade.NormalisedScore, grade.Feedback).Should().Be((0m, GradeFeedback.MathWrongForm));
    }

    [Fact]
    public async Task GradeAsync_ClientUnavailable_Propagates()
    {
        _mathCheckClient.CheckAsync(Arg.Any<AiMathCheckRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(new ServiceUnavailableCoreException(ErrorCodes.MathCheckUnavailable));

        var act = () => Grade(MathStepsSpecJson, """{"finalAnswer":"x = 2"}""");

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.MathCheckUnavailable);
    }

    private void Reply(MathAnswerVerdict verdict) => _mathCheckClient.CheckAsync(Arg.Any<AiMathCheckRequest>(), Arg.Any<CancellationToken>()).Returns(new AiMathCheckResult(verdict, null, []));

    private Task<QuestionGrade> Grade(string spec, string answer) => AnswerGrader.GradeAsync(QuestionType.MathSteps, spec, 2, Json(answer), _mathCheckClient, TestContext.Current.CancellationToken);
}
