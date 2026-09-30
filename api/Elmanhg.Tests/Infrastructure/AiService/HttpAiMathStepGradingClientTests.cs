using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Infrastructure.AiService;
using Elmanhg.Tests.Infrastructure.OtpDelivery;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;

namespace Elmanhg.Tests.Infrastructure.AiService;

public sealed class HttpAiMathStepGradingClientTests
{
    private const string ReplyBody = """{"steps":[{"stepIndex":0,"points":2,"justification":"Right."},{"stepIndex":1,"points":1,"justification":"Partly."}],"totalPoints":3,"maxPoints":4,"justification":"Good.","confidence":0.82,"model":"claude-sonnet-5","promptVersion":"v1","inputTokens":900,"outputTokens":150,"stopReason":"end_turn","costUsd":0.00495}""";
    private static readonly AiMathStepGradingRequest Request = new("Solve 2x + 3 = 7.", ["2x = 4", "x = 2"], ["x = 2"], ["2x = 4"], "x = 2", null, []);
    private readonly StubHttpMessageHandler _handler = new() { ResponseBody = ReplyBody };

    [Fact]
    public async Task GradeAsync_Success_PostsCamelCaseRequestWithBearerAndMapsReply()
    {
        var result = await GradeAsync();

        _handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        _handler.LastRequest.RequestUri.Should().Be(new Uri("http://ai.test/v1/math-step-grades"));
        _handler.LastRequest.Headers.Authorization!.ToString().Should().Be($"Bearer {AiServiceTestSettings.ServiceToken}");
        using var body = JsonDocument.Parse(_handler.LastBody!);
        var root = body.RootElement;
        (root.GetProperty("question").GetString(), root.GetProperty("modelSolution")[1].GetString(), root.GetProperty("acceptedAnswers")[0].GetString(), root.GetProperty("steps")[0].GetString(), root.GetProperty("finalAnswer").GetString()).Should().Be(("Solve 2x + 3 = 7.", "x = 2", "x = 2", "2x = 4", "x = 2"));
        root.TryGetProperty("subject", out _).Should().BeFalse();
        result.Steps.Should().Equal(new AiMathStepScore(0, 2, "Right."), new AiMathStepScore(1, 1, "Partly."));
        (result.TotalPoints, result.MaxPoints, result.Justification, result.Confidence, result.Model, result.PromptVersion, result.InputTokens, result.OutputTokens, result.StopReason, result.CostUsd).Should().Be((3, 4, "Good.", 0.82m, "claude-sonnet-5", "v1", 900, 150, "end_turn", 0.00495m));
    }

    [Fact]
    public async Task GradeAsync_NonSuccessStatus_ThrowsMathStepGradingUnavailable()
    {
        _handler.StatusCode = HttpStatusCode.BadGateway;

        await ExpectUnavailableAsync();
    }

    [Fact]
    public async Task GradeAsync_TransportFailure_ThrowsMathStepGradingUnavailable()
    {
        _handler.Throw = new HttpRequestException();

        var exception = await ExpectUnavailableAsync();

        exception.InnerException.Should().BeOfType<HttpRequestException>();
    }

    [Theory]
    [InlineData("null")]
    [InlineData("not json")]
    [InlineData("""{"steps":[{"stepIndex":0,"points":2,"justification":"Right."}],"totalPoints":2,"maxPoints":4,"justification":"Good.","confidence":0.82,"model":"m","promptVersion":"v1","inputTokens":1,"outputTokens":1,"costUsd":0}""")]
    [InlineData("""{"steps":[{"stepIndex":0,"points":2,"justification":"Right."},{"stepIndex":1,"points":1,"justification":"Partly."}],"totalPoints":4,"maxPoints":4,"justification":"Good.","confidence":0.82,"model":"m","promptVersion":"v1","inputTokens":1,"outputTokens":1,"costUsd":0}""")]
    public async Task GradeAsync_InvalidReply_ThrowsMathStepGradingUnavailable(string body)
    {
        _handler.ResponseBody = body;

        await ExpectUnavailableAsync();
    }

    private async Task<ServiceUnavailableCoreException> ExpectUnavailableAsync()
    {
        var act = () => GradeAsync();

        var exception = (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which;
        exception.ErrorCode.Should().Be(ErrorCodes.MathStepGradingUnavailable);
        return exception;
    }

    private Task<AiMathStepGradingResult> GradeAsync()
    {
        var client = new HttpAiMathStepGradingClient(new HttpClient(_handler) { BaseAddress = new Uri("http://ai.test/") }, Options.Create(AiServiceTestSettings.WithHttp()), NullLogger<HttpAiMathStepGradingClient>.Instance);
        return client.GradeAsync(Request, TestContext.Current.CancellationToken);
    }
}
