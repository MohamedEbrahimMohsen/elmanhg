using Elmanhg.Application.Shared.AiService;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Questions.Schemas;
using Elmanhg.Infrastructure.AiService;
using Elmanhg.Tests.Infrastructure.OtpDelivery;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;

namespace Elmanhg.Tests.Infrastructure.AiService;

public sealed class HttpAiMathCheckClientTests
{
    private const string ReplyBody = """{"verdict":"notEquivalent","matchedIndex":null,"invalidExpected":[1]}""";
    private readonly StubHttpMessageHandler _handler = new() { ResponseBody = ReplyBody };

    [Fact]
    public async Task CheckAsync_Success_PostsCamelCaseBodyWithBearerAndMapsReply()
    {
        var result = await CheckAsync(new AiMathCheckRequest("x=2", ["x = 3", "x +"], MathAnswerForm.Factored, null, null));

        _handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        _handler.LastRequest.RequestUri.Should().Be(new Uri("http://ai.test/v1/math-checks"));
        _handler.LastRequest.Headers.Authorization!.ToString().Should().Be($"Bearer {AiServiceTestSettings.ServiceToken}");
        using var body = JsonDocument.Parse(_handler.LastBody!);
        var root = body.RootElement;
        (root.GetProperty("answer").GetString(), root.GetProperty("expected").GetArrayLength(), root.GetProperty("form").GetString()).Should().Be(("x=2", 2, "factored"));
        root.TryGetProperty("tolerance", out _).Should().BeFalse();
        root.TryGetProperty("toleranceMode", out _).Should().BeFalse();
        (result.Verdict, result.MatchedIndex).Should().Be((MathAnswerVerdict.NotEquivalent, (int?)null));
        result.InvalidExpected.Should().Equal(1);
    }

    [Fact]
    public async Task CheckAsync_WithTolerance_SendsToleranceAndMode()
    {
        await CheckAsync(new AiMathCheckRequest("1.41", ["\\sqrt{2}"], MathAnswerForm.Equivalent, 0.01m, ToleranceMode.Absolute));

        using var body = JsonDocument.Parse(_handler.LastBody!);
        (body.RootElement.GetProperty("tolerance").GetDecimal(), body.RootElement.GetProperty("toleranceMode").GetString()).Should().Be((0.01m, "absolute"));
    }

    [Fact]
    public async Task CheckAsync_Non2xx_ReturnsUnchecked()
    {
        _handler.StatusCode = HttpStatusCode.ServiceUnavailable;

        await ExpectUncheckedAsync();
    }

    [Fact]
    public async Task CheckAsync_TransportFailure_ReturnsUnchecked()
    {
        _handler.Throw = new HttpRequestException();

        await ExpectUncheckedAsync();
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{"verdict":"maybe","invalidExpected":[]}""")]
    [InlineData("""{"verdict":99,"invalidExpected":[]}""")]
    [InlineData("""{"verdict":"equivalent"}""")]
    public async Task CheckAsync_InvalidReply_ReturnsUnchecked(string body)
    {
        _handler.ResponseBody = body;

        await ExpectUncheckedAsync();
    }

    private async Task ExpectUncheckedAsync()
    {
        var result = await CheckAsync(new AiMathCheckRequest("x=2", ["x = 2"], MathAnswerForm.Equivalent, null, null));

        (result.Verdict, result.MatchedIndex, result.InvalidExpected.Count).Should().Be((MathAnswerVerdict.Unchecked, (int?)null, 0));
    }

    private Task<AiMathCheckResult> CheckAsync(AiMathCheckRequest request)
    {
        var client = new HttpAiMathCheckClient(new HttpClient(_handler) { BaseAddress = new Uri("http://ai.test/") }, Options.Create(AiServiceTestSettings.WithHttp()), NullLogger<HttpAiMathCheckClient>.Instance);
        return client.CheckAsync(request, TestContext.Current.CancellationToken);
    }
}
