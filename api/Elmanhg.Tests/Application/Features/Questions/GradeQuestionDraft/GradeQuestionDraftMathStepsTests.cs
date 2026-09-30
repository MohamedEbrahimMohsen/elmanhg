using Core.Errors;
using Core.Localization;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.MathStepGrading.Shared;
using Elmanhg.Application.Questions.GradeQuestionDraft;
using Elmanhg.Application.Questions.Shared.Grading;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.RichText;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using Elmanhg.Infrastructure.RichText;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using static Elmanhg.Tests.Builders.QuestionBuilder;

namespace Elmanhg.Tests.Application.Features.Questions.GradeQuestionDraft;

public sealed class GradeQuestionDraftMathStepsTests
{
    private const string Answer = """{"steps":["2x = 4"],"finalAnswer":"x = 2"}""";
    private readonly IAiMathCheckClient _mathCheckClient = Substitute.For<IAiMathCheckClient>();
    private readonly IAiMathStepGradingClient _mathStepGradingClient = Substitute.For<IAiMathStepGradingClient>();
    private readonly IRichTextSanitizer _richTextSanitizer = Substitute.For<IRichTextSanitizer>();
    private readonly ILocalizer _localizer = Substitute.For<ILocalizer>();
    private readonly GradeQuestionDraftHandler _handler;

    public GradeQuestionDraftMathStepsTests()
    {
        _richTextSanitizer.Sanitize(Arg.Any<string?>()).Returns(x => x.Arg<string?>() ?? string.Empty);
        _localizer.GetMessage(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Dictionary<string, object>?>()).Returns(call => call.ArgAt<string?>(0));
        _mathCheckClient.CheckAsync(Arg.Any<AiMathCheckRequest>(), Arg.Any<CancellationToken>()).Returns(new AiMathCheckResult(MathAnswerVerdict.Equivalent, 0, []));
        _mathStepGradingClient.GradeAsync(Arg.Any<AiMathStepGradingRequest>(), Arg.Any<CancellationToken>()).Returns(new AiMathStepGradingResult([new AiMathStepScore(0, 2, "Right."), new AiMathStepScore(1, 1, "Partly.")], 3, 4, "Good.", 0.8m, "claude-sonnet-5", "v1", 900, 150, "end_turn", 0.004m));
        _handler = new GradeQuestionDraftHandler(_richTextSanitizer, new RichTextExtractor(), Substitute.For<ILessonRepository>(), Substitute.For<ICurriculumUnitRepository>(), Substitute.For<ISubjectRepository>(), Substitute.For<IAiEssayGradingClient>(), Options.Create(new EssayGradingOptions()), _localizer, _mathCheckClient, Options.Create(new SessionsOptions()), _mathStepGradingClient, Options.Create(new MathStepGradingOptions()));
    }

    [Fact]
    public async Task Handle_StepGradedDraft_ReturnsCombinedGradeAndDetail()
    {
        var result = await _handler.Handle(new GradeQuestionDraftQuery(MathStepsGradedFields(), Json(Answer)), TestContext.Current.CancellationToken);

        (result.Score, result.NormalisedScore, result.Outcome, result.Feedback).Should().Be((1.75m, 0.875m, "Partial", GradeFeedbackKeys.MathStepTally));
        result.MathSteps!.Steps.Should().Equal(new MathStepScoreResult(0, "2x = 4", 2, 2, "Right."), new MathStepScoreResult(1, "x = 2", 1, 2, "Partly."));
        (result.MathSteps.FinalAnswerVerdict, result.MathSteps.Confidence, result.MathSteps.Justification).Should().Be(("Equivalent", 0.8m, "Good."));
    }

    [Fact]
    public async Task Handle_FinalOnlyDraft_DoesNotCallStepGrader()
    {
        var result = await _handler.Handle(new GradeQuestionDraftQuery(MathStepsFields(), Json(Answer)), TestContext.Current.CancellationToken);

        (result.Score, result.MathSteps).Should().Be((2m, (MathStepGradeDetailResult?)null));
        await _mathStepGradingClient.DidNotReceive().GradeAsync(Arg.Any<AiMathStepGradingRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UncheckedDraft_ReturnsProvisionalZeroWithoutStepGrader()
    {
        _mathCheckClient.CheckAsync(Arg.Any<AiMathCheckRequest>(), Arg.Any<CancellationToken>()).Returns(new AiMathCheckResult(MathAnswerVerdict.Unchecked, null, []));

        var result = await _handler.Handle(new GradeQuestionDraftQuery(MathStepsGradedFields(), Json(Answer)), TestContext.Current.CancellationToken);

        (result.Score, result.Feedback, result.MathSteps).Should().Be((0m, GradeFeedbackKeys.MathUnchecked, (MathStepGradeDetailResult?)null));
        await _mathStepGradingClient.DidNotReceive().GradeAsync(Arg.Any<AiMathStepGradingRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_StepGraderUnavailable_Propagates()
    {
        _mathStepGradingClient.GradeAsync(Arg.Any<AiMathStepGradingRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(new ServiceUnavailableCoreException(ErrorCodes.MathStepGradingUnavailable));

        var act = () => _handler.Handle(new GradeQuestionDraftQuery(MathStepsGradedFields(), Json(Answer)), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.MathStepGradingUnavailable);
    }

    [Fact]
    public async Task Handle_UnknownLesson_ThrowsLessonNotFound()
    {
        var act = () => _handler.Handle(new GradeQuestionDraftQuery(MathStepsGradedFields(), Json(Answer), Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonNotFound);
        await _mathStepGradingClient.DidNotReceive().GradeAsync(Arg.Any<AiMathStepGradingRequest>(), Arg.Any<CancellationToken>());
    }
}
