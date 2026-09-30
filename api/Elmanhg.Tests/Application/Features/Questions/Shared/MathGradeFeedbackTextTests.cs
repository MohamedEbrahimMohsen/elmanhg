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
}
