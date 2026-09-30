using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Infrastructure.AiService;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Elmanhg.Tests.Infrastructure.AiService;

public sealed class FakeAiEssayGradingClientTests
{
    private readonly IHostEnvironment _hostEnvironment = Substitute.For<IHostEnvironment>();

    [Fact]
    public async Task GradeAsync_AwardsFullPointsPerCriterion()
    {
        _hostEnvironment.EnvironmentName.Returns(Environments.Development);

        var result = await Grade();

        result.Criteria.Select(x => (x.CriterionId, x.Points)).Should().Equal(("c1", 2), ("c2", 3));
        (result.TotalPoints, result.MaxPoints, result.Confidence, result.Model, result.CostUsd).Should().Be((5, 5, 0.9m, "fake", 0m));
    }

    [Fact]
    public async Task GradeAsync_Production_ThrowsEssayGradingUnavailable()
    {
        _hostEnvironment.EnvironmentName.Returns(Environments.Production);

        var act = () => Grade();

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.EssayGradingUnavailable);
    }

    private Task<AiEssayGradingResult> Grade()
    {
        AiRubricLevel[] levels = [new(0, "None"), new(2, "Full")];
        var request = new AiEssayGradingRequest("Q", [new("c1", "Definition", null, 2, levels), new("c2", "Example", null, 3, levels)], ["Model"], "Essay", null, []);
        return new FakeAiEssayGradingClient(_hostEnvironment).GradeAsync(request, TestContext.Current.CancellationToken);
    }
}
