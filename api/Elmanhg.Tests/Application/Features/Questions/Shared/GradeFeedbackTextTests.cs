using Core.Localization;
using Elmanhg.Application.Questions.Shared.Grading;
using Elmanhg.Domain.Questions.Grading;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Questions.Shared;

public sealed class GradeFeedbackTextTests
{
    private readonly ILocalizer _localizer = Substitute.For<ILocalizer>();

    public GradeFeedbackTextTests()
    {
        _localizer.GetMessage(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Dictionary<string, object>?>()).Returns(string.Empty);
    }

    [Fact]
    public void Localize_NullFeedback_ReturnsNull()
    {
        var text = GradeFeedbackText.Localize(null, _localizer);

        text.Should().BeNull();
        _localizer.DidNotReceiveWithAnyArgs().GetMessage(default);
    }

    [Fact]
    public void Localize_Unanswered_UsesUnansweredKey()
    {
        _localizer.GetMessage(GradeFeedbackKeys.Unanswered, Arg.Any<string?>(), Arg.Any<Dictionary<string, object>?>()).Returns("unanswered");

        GradeFeedbackText.Localize(GradeFeedback.Unanswered, _localizer).Should().Be("unanswered");
    }

    [Fact]
    public void Localize_ChoiceTally_PassesRightWrongAndTotal()
    {
        _localizer.GetMessage(GradeFeedbackKeys.ChoiceTally, Arg.Any<string?>(), Arg.Is<Dictionary<string, object>?>(x => x != null && x["right"].Equals(2) && x["wrong"].Equals(1) && x["total"].Equals(3))).Returns("tally");

        GradeFeedbackText.Localize(GradeFeedback.ChoiceTally(2, 1, 3), _localizer).Should().Be("tally");
    }

    [Fact]
    public void Localize_UndefinedKind_ThrowsInvalidOperation()
    {
        var act = () => GradeFeedbackText.Localize(new GradeFeedback((GradeFeedbackKind)99, 0, 0, 0), _localizer);

        act.Should().Throw<InvalidOperationException>().WithMessage("Unsupported grade feedback.");
    }
}
