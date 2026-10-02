using Elmanhg.Application.Shared.Messaging;
using Elmanhg.Infrastructure.Messaging;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;

namespace Elmanhg.Tests.Infrastructure.Messaging;

public sealed class FakeMessageChannelTests
{
    private const string Address = "01012345678";
    private static readonly TeacherThreadReminderMessage Reminder = new(Guid.NewGuid(), Address, "Mona", Guid.NewGuid(), "Physics", "Newton's laws", new DateTimeOffset(2026, 12, 1, 10, 0, 0, TimeSpan.Zero));

    private readonly FakeLogger<FakeMessageChannel> _logger = new();

    [Fact]
    public async Task SendAsync_Development_LogsInformationWithoutAddress()
    {
        var sent = await Channel(Environments.Development).SendAsync(Reminder, TestContext.Current.CancellationToken);

        sent.Should().BeTrue();
        var entry = _logger.Collector.GetSnapshot().Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Information);
        entry.Message.Should().Contain(Reminder.RecipientUserId.ToString()).And.NotContain(Address);
    }

    [Fact]
    public async Task SendAsync_OutsideDevelopment_LogsWarning()
    {
        var sent = await Channel(Environments.Production).SendAsync(Reminder, TestContext.Current.CancellationToken);

        sent.Should().BeTrue();
        _logger.Collector.GetSnapshot().Should().ContainSingle().Which.Level.Should().Be(LogLevel.Warning);
    }

    private FakeMessageChannel Channel(string environmentName)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        return new FakeMessageChannel(MessageChannel.WhatsApp, _logger, environment);
    }
}
