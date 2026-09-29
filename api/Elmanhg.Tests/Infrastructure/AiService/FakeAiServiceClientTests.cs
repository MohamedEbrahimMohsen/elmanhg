using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Infrastructure.AiService;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Elmanhg.Tests.Infrastructure.AiService;

public sealed class FakeAiServiceClientTests
{
    private readonly IHostEnvironment _hostEnvironment = Substitute.For<IHostEnvironment>();

    [Fact]
    public async Task ChatAsync_Development_ReturnsFakeReply()
    {
        _hostEnvironment.EnvironmentName.Returns(Environments.Development);

        var reply = await new FakeAiServiceClient(_hostEnvironment).ChatAsync(AiServiceTestSettings.ChatRequest(), TestContext.Current.CancellationToken);

        reply.Reply.Should().Be(FakeAiServiceClient.FakeReply);
        reply.Model.Should().Be("fake");
    }

    [Fact]
    public async Task ChatAsync_Production_ThrowsAiServiceUnavailable()
    {
        _hostEnvironment.EnvironmentName.Returns(Environments.Production);

        var act = () => new FakeAiServiceClient(_hostEnvironment).ChatAsync(AiServiceTestSettings.ChatRequest(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.AiServiceUnavailable);
    }
}
