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
    public async Task DecideAsync_NonMathType_GradesWithoutClient()
    {
        var decision = await AnswerGrader.DecideAsync(QuestionType.Mcq, """{"correctOptionId":"b"}""", 1, Json("""{"optionId":"b"}"""), _mathCheckClient, TestContext.Current.CancellationToken);

        decision.Grade!.Outcome.Should().Be(GradeOutcome.Correct);
        await _mathCheckClient.DidNotReceive().CheckAsync(Arg.Any<AiMathCheckRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DecideAsync_BlankFinalAnswer_ReturnsUnansweredWithoutClient()
    {
        var grade = await Grade(MathStepsSpecJson, """{"steps":["x"],"finalAnswer":"  "}""");

        (grade.NormalisedScore, grade.Feedback).Should().Be((0m, GradeFeedback.Unanswered));
        await _mathCheckClient.DidNotReceive().CheckAsync(Arg.Any<AiMathCheckRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DecideAsync_MathAnswer_SendsTrimmedFinalAnswerAndRules()
    {
        Reply(MathAnswerVerdict.Equivalent);

        var grade = await Grade(ToleranceSpec, """{"steps":["2x = 4"],"finalAnswer":"  x = 2 "}""");

        await _mathCheckClient.Received(1).CheckAsync(Arg.Is<AiMathCheckRequest>(x => x.Answer == "x = 2" && x.Expected.SequenceEqual(new[] { "x = 2" }) && x.Form == MathAnswerForm.Equivalent && x.Tolerance == 0.01m && x.ToleranceMode == ToleranceMode.Absolute), Arg.Any<CancellationToken>());
        (grade.Outcome, grade.Score).Should().Be((GradeOutcome.Correct, 2m));
    }

    [Fact]
    public async Task DecideAsync_WrongFormVerdict_ReturnsZeroWithWrongFormFeedback()
    {
        Reply(MathAnswerVerdict.WrongForm);

        var grade = await Grade(MathStepsSpecJson, """{"finalAnswer":"x = 4/2"}""");

        (grade.NormalisedScore, grade.Feedback).Should().Be((0m, GradeFeedback.MathWrongForm));
    }

    [Fact]
    public async Task DecideAsync_ClientUnavailable_Propagates()
    {
        _mathCheckClient.CheckAsync(Arg.Any<AiMathCheckRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(new ServiceUnavailableCoreException(ErrorCodes.MathCheckUnavailable));

        var act = () => Grade(MathStepsSpecJson, """{"finalAnswer":"x = 2"}""");

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.MathCheckUnavailable);
    }

    private void Reply(MathAnswerVerdict verdict) => _mathCheckClient.CheckAsync(Arg.Any<AiMathCheckRequest>(), Arg.Any<CancellationToken>()).Returns(new AiMathCheckResult(verdict, null, []));

    [Fact]
    public async Task DecideAsync_UncheckedVerdict_DefersWithoutVerdict()
    {
        Reply(MathAnswerVerdict.Unchecked);

        var decision = await Decide(MathStepsSpecJson, """{"steps":["2x = 4"],"finalAnswer":"x = 2"}""");

        decision.Should().Be(AnswerDecision.Deferred(null));
    }

    [Fact]
    public async Task DecideAsync_StepGradedWithSteps_DefersWithVerdict()
    {
        Reply(MathAnswerVerdict.Equivalent);

        var decision = await Decide(MathStepsGradedSpecJson, """{"steps":["2x = 4"],"finalAnswer":"x = 2"}""");

        (decision.Grade, decision.Verdict).Should().Be(((QuestionGrade?)null, (MathAnswerVerdict?)MathAnswerVerdict.Equivalent));
    }

    [Fact]
    public async Task DecideAsync_StepGradedWithoutSteps_GradesWithZeroStepCredit()
    {
        Reply(MathAnswerVerdict.Equivalent);

        var grade = await Grade(MathStepsGradedSpecJson, """{"steps":["  "],"finalAnswer":"x = 2"}""");

        grade.Should().Be(new QuestionGrade(1m, 0.5m, GradeOutcome.Partial, GradeFeedback.MathStepTally(0, 2)));
    }

    private Task<AnswerDecision> Decide(string spec, string answer) => AnswerGrader.DecideAsync(QuestionType.MathSteps, spec, 2, Json(answer), _mathCheckClient, TestContext.Current.CancellationToken);

    private async Task<QuestionGrade> Grade(string spec, string answer) => (await Decide(spec, answer).ConfigureAwait(false)).Grade!;
}
