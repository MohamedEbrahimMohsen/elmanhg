using Elmanhg.Application.Shared.AiService;
using Elmanhg.Infrastructure.AiService;
using FluentAssertions;

namespace Elmanhg.Tests.Infrastructure.AiService;

public sealed class AiMathStepGradingReplyRulesTests
{
    private static readonly AiMathStepGradingRequest Request = new("Q", ["2x = 4", "x = 2"], ["x = 2"], ["2x = 4"], "x = 2", null, []);
    private static readonly AiMathStepGradingResult Valid = new([new AiMathStepScore(0, 2, "Right."), new AiMathStepScore(1, 1, "Partly.")], 3, 4, "Good.", 0.8m, "claude-sonnet-5", "v1", 900, 150, "end_turn", 0.004m);

    [Fact]
    public void IsValid_ConsistentReply_ReturnsTrue()
    {
        AiMathStepGradingReplyRules.IsValid(Request, Valid).Should().BeTrue();
    }

    [Theory]
    [InlineData("missing-step")]
    [InlineData("duplicate-index")]
    [InlineData("points-3")]
    [InlineData("wrong-total")]
    [InlineData("wrong-max")]
    [InlineData("confidence-1.5")]
    [InlineData("blank-justification")]
    [InlineData("blank-model")]
    public void IsValid_BrokenReply_ReturnsFalse(string broken)
    {
        var reply = broken switch
        {
            "missing-step" => Valid with { Steps = [new AiMathStepScore(0, 2, "Right.")], TotalPoints = 2 },
            "duplicate-index" => Valid with { Steps = [new AiMathStepScore(0, 2, "Right."), new AiMathStepScore(0, 1, "Partly.")] },
            "points-3" => Valid with { Steps = [new AiMathStepScore(0, 3, "Right."), new AiMathStepScore(1, 0, "Missing.")] },
            "wrong-total" => Valid with { TotalPoints = 4 },
            "wrong-max" => Valid with { MaxPoints = 6 },
            "confidence-1.5" => Valid with { Confidence = 1.5m },
            "blank-justification" => Valid with { Steps = [new AiMathStepScore(0, 2, " "), new AiMathStepScore(1, 1, "Partly.")] },
            "blank-model" => Valid with { Model = " " },
            _ => throw new ArgumentOutOfRangeException(nameof(broken)),
        };

        AiMathStepGradingReplyRules.IsValid(Request, reply).Should().BeFalse();
    }
}
