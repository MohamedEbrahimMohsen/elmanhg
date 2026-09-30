using Core.Localization;
using Elmanhg.Application.Questions.Shared.Grading;
using Elmanhg.Domain.Questions.Grading;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Questions.Shared;

public sealed class MathGradeFeedbackTextTests
{
    private readonly ILocalizer _localizer = Substitute.For<ILocalizer>();

    [Theory]
    [InlineData(GradeFeedbackKind.MathFinalAnswerOnly, GradeFeedbackKeys.MathFinalAnswerOnly)]
    [InlineData(GradeFeedbackKind.MathWrongForm, GradeFeedbackKeys.MathWrongForm)]
    [InlineData(GradeFeedbackKind.MathUnreadable, GradeFeedbackKeys.MathUnreadable)]
    [InlineData(GradeFeedbackKind.MathUnchecked, GradeFeedbackKeys.MathUnchecked)]
    public void Localize_MathKinds_UseTheirKeys(GradeFeedbackKind kind, string key)
    {
        _localizer.GetMessage(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Dictionary<string, object>?>()).Returns(call => call.ArgAt<string?>(0));

        var text = GradeFeedbackText.Localize(new GradeFeedback(kind, 0, 0, 0), _localizer);

        text.Should().Be(key);
    }

    [Fact]
    public void Localize_MathStepTally_PassesRightAndTotal()
    {
        _localizer.GetMessage(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Dictionary<string, object>?>()).Returns(call => call.ArgAt<string?>(0));

        var text = GradeFeedbackText.Localize(GradeFeedback.MathStepTally(1, 3), _localizer);

        text.Should().Be(GradeFeedbackKeys.MathStepTally);
        _localizer.Received(1).GetMessage(GradeFeedbackKeys.MathStepTally, Arg.Any<string?>(), Arg.Is<Dictionary<string, object>?>(x => x != null && x.Count == 2 && (int)x[GradeFeedbackKeys.RightArgument] == 1 && (int)x[GradeFeedbackKeys.TotalArgument] == 3));
    }
}
