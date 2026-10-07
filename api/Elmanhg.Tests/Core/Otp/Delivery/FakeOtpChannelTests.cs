using Core.OTP.Delivery;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;

namespace Elmanhg.Tests.Core.Otp.Delivery;

public sealed class FakeOtpChannelTests
{
    private const string Phone = "01012345678";
    private const string Code = "482913";

    private readonly FakeLogger<FakeOtpChannel> _logger = new();

    [Fact]
    public async Task SendAsync_Development_LogsCodeAtInformation()
    {
        await Channel(Environments.Development).SendAsync(Phone, Code, TestContext.Current.CancellationToken);

        var entry = _logger.Collector.GetSnapshot().Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Information);
        entry.Message.Should().Contain(Code);
    }

    [Fact]
    public async Task SendAsync_OutsideDevelopment_LogsWarningWithoutCode()
    {
        await Channel(Environments.Production).SendAsync(Phone, Code, TestContext.Current.CancellationToken);

        var entry = _logger.Collector.GetSnapshot().Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Warning);
        entry.Message.Should().NotContain(Code);
    }

    private FakeOtpChannel Channel(string environmentName)
    {
        var hostEnvironment = Substitute.For<IHostEnvironment>();
        hostEnvironment.EnvironmentName.Returns(environmentName);
        return new FakeOtpChannel(OtpChannel.WhatsApp, _logger, hostEnvironment);
    }
}
