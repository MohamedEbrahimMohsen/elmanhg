using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Infrastructure.AiService;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Elmanhg.Tests.Infrastructure.AiService;

public sealed class FakeAiTranscriptionClientTests
{
    private readonly IHostEnvironment _hostEnvironment = Substitute.For<IHostEnvironment>();

    [Fact]
    public async Task TranscribeAsync_Development_ReturnsFixedTranscript()
    {
        _hostEnvironment.EnvironmentName.Returns(Environments.Development);

        var result = await Transcribe();

        result.Should().Be(new AiTranscriptionResult(FakeAiTranscriptionClient.FakeTranscript, "fake", "en"));
    }

    [Fact]
    public async Task TranscribeAsync_Production_ThrowsAiServiceUnavailable()
    {
        _hostEnvironment.EnvironmentName.Returns(Environments.Production);

        var act = () => Transcribe();

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.AiServiceUnavailable);
    }

    private Task<AiTranscriptionResult> Transcribe() => new FakeAiTranscriptionClient(_hostEnvironment).TranscribeAsync(new AiTranscriptionRequest([1, 2, 3], "audio/webm", "en", 3), TestContext.Current.CancellationToken);
}
