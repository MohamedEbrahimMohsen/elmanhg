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

public sealed class HttpAiEssayGradingClientTests
{
    private const string ReplyBody = """{"criteria":[{"criterionId":"c1","points":1,"justification":"Partly."},{"criterionId":"c2","points":3,"justification":"Clear."}],"totalPoints":4,"maxPoints":5,"justification":"Good.","confidence":0.82,"model":"claude-sonnet-5","promptVersion":"v1","inputTokens":900,"outputTokens":150,"stopReason":"end_turn","costUsd":0.00495}""";
    private static readonly AiEssayGradingRequest Request = new("Explain inertia.", [new("c1", "Definition", null, 2, [new(0, "Missing"), new(2, "Complete")]), new("c2", "Example", "An example", 3, [new(0, "None"), new(3, "Clear")])], ["Inertia is resistance."], "Essay text", null, []);
    private readonly StubHttpMessageHandler _handler = new() { ResponseBody = ReplyBody };

    [Fact]
    public async Task GradeAsync_Success_PostsCamelCaseRubricWithBearerAndMapsReply()
    {
        var result = await GradeAsync();

        _handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        _handler.LastRequest.RequestUri.Should().Be(new Uri("http://ai.test/v1/essay-grades"));
        _handler.LastRequest.Headers.Authorization!.ToString().Should().Be($"Bearer {AiServiceTestSettings.ServiceToken}");
        using var body = JsonDocument.Parse(_handler.LastBody!);
        var root = body.RootElement;
        (root.GetProperty("question").GetString(), root.GetProperty("criteria")[0].GetProperty("levels")[0].GetProperty("points").GetInt32(), root.GetProperty("modelAnswers")[0].GetString(), root.GetProperty("essay").GetString()).Should().Be(("Explain inertia.", 0, "Inertia is resistance.", "Essay text"));
        root.TryGetProperty("subject", out _).Should().BeFalse();
        result.Criteria.Should().Equal(new AiEssayCriterionScore("c1", 1, "Partly."), new AiEssayCriterionScore("c2", 3, "Clear."));
        (result.TotalPoints, result.MaxPoints, result.Justification, result.Confidence, result.Model, result.PromptVersion, result.InputTokens, result.OutputTokens, result.StopReason, result.CostUsd).Should().Be((4, 5, "Good.", 0.82m, "claude-sonnet-5", "v1", 900, 150, "end_turn", 0.00495m));
    }

    [Fact]
    public async Task GradeAsync_Non2xx_ThrowsEssayGradingUnavailable()
    {
        _handler.StatusCode = HttpStatusCode.BadGateway;

        await ExpectUnavailableAsync();
    }

    [Fact]
    public async Task GradeAsync_TransportFailure_ThrowsEssayGradingUnavailable()
    {
        _handler.Throw = new HttpRequestException();

        var exception = await ExpectUnavailableAsync();

        exception.InnerException.Should().BeOfType<HttpRequestException>();
    }

    [Theory]
    [InlineData("null")]
    [InlineData("not json")]
    [InlineData("""{"criteria":[{"criterionId":"c1","points":1,"justification":"Partly."}],"totalPoints":1,"maxPoints":5,"justification":"Good.","confidence":0.82,"model":"m","promptVersion":"v1","inputTokens":1,"outputTokens":1,"costUsd":0}""")]
    [InlineData("""{"criteria":[{"criterionId":"c1","points":1,"justification":"Partly."},{"criterionId":"c9","points":3,"justification":"Clear."}],"totalPoints":4,"maxPoints":5,"justification":"Good.","confidence":0.82,"model":"m","promptVersion":"v1","inputTokens":1,"outputTokens":1,"costUsd":0}""")]
    [InlineData("""{"criteria":[{"criterionId":"c1","points":3,"justification":"Partly."},{"criterionId":"c2","points":3,"justification":"Clear."}],"totalPoints":6,"maxPoints":5,"justification":"Good.","confidence":0.82,"model":"m","promptVersion":"v1","inputTokens":1,"outputTokens":1,"costUsd":0}""")]
    [InlineData("""{"criteria":[{"criterionId":"c1","points":1,"justification":"Partly."},{"criterionId":"c2","points":3,"justification":"Clear."}],"totalPoints":4,"maxPoints":5,"justification":"Good.","confidence":1.5,"model":"m","promptVersion":"v1","inputTokens":1,"outputTokens":1,"costUsd":0}""")]
    [InlineData("""{"criteria":[{"criterionId":"c1","points":1,"justification":"Partly."},{"criterionId":"c2","points":3,"justification":"Clear."}],"totalPoints":4,"maxPoints":5,"justification":"Good.","confidence":0.82,"model":" ","promptVersion":"v1","inputTokens":1,"outputTokens":1,"costUsd":0}""")]
    [InlineData("""{"criteria":[{"criterionId":"c1","points":1,"justification":"Partly."},{"criterionId":"c2","points":3,"justification":"Clear."}],"totalPoints":5,"maxPoints":5,"justification":"Good.","confidence":0.82,"model":"m","promptVersion":"v1","inputTokens":1,"outputTokens":1,"costUsd":0}""")]
    public async Task GradeAsync_InvalidReply_ThrowsEssayGradingUnavailable(string body)
    {
        _handler.ResponseBody = body;

        await ExpectUnavailableAsync();
    }

    private async Task<ServiceUnavailableCoreException> ExpectUnavailableAsync()
    {
        var act = () => GradeAsync();

        var exception = (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which;
        exception.ErrorCode.Should().Be(ErrorCodes.EssayGradingUnavailable);
        return exception;
    }

    private Task<AiEssayGradingResult> GradeAsync()
    {
        var client = new HttpAiEssayGradingClient(new HttpClient(_handler) { BaseAddress = new Uri("http://ai.test/") }, Options.Create(AiServiceTestSettings.WithHttp()), NullLogger<HttpAiEssayGradingClient>.Instance);
        return client.GradeAsync(Request, TestContext.Current.CancellationToken);
    }
}
