using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Infrastructure.AiService;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Elmanhg.Tests.Infrastructure.AiService;

public sealed class FakeAiMathStepGradingClientTests
{
    private readonly IHostEnvironment _hostEnvironment = Substitute.For<IHostEnvironment>();

    [Fact]
    public async Task GradeAsync_AwardsFullPointsPerModelStep()
    {
        _hostEnvironment.EnvironmentName.Returns(Environments.Development);

        var result = await Grade();

        result.Steps.Select(x => (x.StepIndex, x.Points)).Should().Equal((0, 2), (1, 2), (2, 2));
        (result.TotalPoints, result.MaxPoints, result.Confidence, result.Model, result.CostUsd).Should().Be((6, 6, 0.9m, "fake", 0m));
        AiMathStepGradingReplyRules.IsValid(Request, result).Should().BeTrue();
    }

    [Fact]
    public async Task GradeAsync_Production_ThrowsMathStepGradingUnavailable()
    {
        _hostEnvironment.EnvironmentName.Returns(Environments.Production);

        var act = () => Grade();

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.MathStepGradingUnavailable);
    }

    private static readonly AiMathStepGradingRequest Request = new("Q", ["a", "b", "c"], ["c"], ["a"], "c", null, []);

    private Task<AiMathStepGradingResult> Grade() => new FakeAiMathStepGradingClient(_hostEnvironment).GradeAsync(Request, TestContext.Current.CancellationToken);
}
