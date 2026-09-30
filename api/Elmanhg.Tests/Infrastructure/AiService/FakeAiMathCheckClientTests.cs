using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Questions.Schemas;
using Elmanhg.Infrastructure.AiService;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Elmanhg.Tests.Infrastructure.AiService;

public sealed class FakeAiMathCheckClientTests
{
    private const char ArabicTwo = (char)0x0662;
    private readonly IHostEnvironment _hostEnvironment = Substitute.For<IHostEnvironment>();

    public FakeAiMathCheckClientTests()
    {
        _hostEnvironment.EnvironmentName.Returns(Environments.Development);
    }

    [Fact]
    public async Task CheckAsync_WhitespaceDifferent_ReturnsEquivalentWithIndex()
    {
        var result = await Check("x=2", "x = 3", "x = 2");

        (result.Verdict, result.MatchedIndex).Should().Be((MathAnswerVerdict.Equivalent, (int?)1));
    }

    [Fact]
    public async Task CheckAsync_DifferentAnswer_ReturnsNotEquivalent()
    {
        var result = await Check("x=5", "x = 2");

        (result.Verdict, result.MatchedIndex).Should().Be((MathAnswerVerdict.NotEquivalent, (int?)null));
    }

    [Fact]
    public async Task CheckAsync_ArabicDigitsAndLeftRight_Normalised()
    {
        var result = await Check($"x=\\left({ArabicTwo}\\right)", "x = (2)");

        result.Verdict.Should().Be(MathAnswerVerdict.Equivalent);
    }

    [Fact]
    public async Task CheckAsync_Production_ThrowsMathCheckUnavailable()
    {
        _hostEnvironment.EnvironmentName.Returns(Environments.Production);

        var act = () => Check("x=2", "x = 2");

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.MathCheckUnavailable);
    }

    private Task<AiMathCheckResult> Check(string answer, params string[] expected) => new FakeAiMathCheckClient(_hostEnvironment).CheckAsync(new AiMathCheckRequest(answer, expected, MathAnswerForm.Equivalent, null, null), TestContext.Current.CancellationToken);
}
