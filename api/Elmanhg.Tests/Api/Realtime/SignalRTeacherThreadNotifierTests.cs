using Elmanhg.Api.Realtime;
using Elmanhg.Domain.TeacherThreads;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Elmanhg.Tests.Api.Realtime;

public sealed class SignalRTeacherThreadNotifierTests
{
    private readonly IHubContext<NotificationsHub> _hubContext = Substitute.For<IHubContext<NotificationsHub>>();
    private readonly IClientProxy _clients = Substitute.For<IClientProxy>();
    private readonly ILogger<SignalRTeacherThreadNotifier> _logger = Substitute.For<ILogger<SignalRTeacherThreadNotifier>>();
    private readonly SignalRTeacherThreadNotifier _notifier;

    public SignalRTeacherThreadNotifierTests() => _notifier = new SignalRTeacherThreadNotifier(_hubContext, _logger);

    [Fact]
    public async Task NotifyReplyAsync_SendsReplyEventToStudent()
    {
        var studentId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        _hubContext.Clients.User(studentId.ToString()).Returns(_clients);

        await _notifier.NotifyReplyAsync(studentId, threadId, TestContext.Current.CancellationToken);

        await _clients.Received(1).SendCoreAsync("teacherReplyReceived", Arg.Is<object?[]>(x => x.Length == 1 && Equals(x[0], new TeacherReplyReceivedMessage(threadId))), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyReminderAsync_SendsReminderEventToTeachers()
    {
        Guid[] teacherIds = [Guid.NewGuid(), Guid.NewGuid()];
        var threadId = Guid.NewGuid();
        IReadOnlyList<string>? users = null;
        _hubContext.Clients.Users(Arg.Do<IReadOnlyList<string>>(x => users = x)).Returns(_clients);

        await _notifier.NotifyReminderAsync(teacherIds, threadId, TeacherThreadSlaEventKind.SecondReminder, TestContext.Current.CancellationToken);

        users.Should().Equal(teacherIds.Select(x => x.ToString()));
        await _clients.Received(1).SendCoreAsync("teacherThreadReminder", Arg.Is<object?[]>(x => x.Length == 1 && Equals(x[0], new TeacherThreadReminderMessage(threadId, TeacherThreadSlaEventKind.SecondReminder))), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyReplyAsync_SendThrows_SwallowsAndLogsWarning()
    {
        var studentId = Guid.NewGuid();
        _hubContext.Clients.User(studentId.ToString()).Returns(_clients);
        _clients.SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>()).ThrowsAsync(new IOException("backplane down"));

        var act = () => _notifier.NotifyReplyAsync(studentId, Guid.NewGuid(), TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
        LoggedLevels().Should().Equal(LogLevel.Warning);
    }

    [Fact]
    public async Task NotifyReminderAsync_SendThrows_SwallowsAndLogsWarning()
    {
        _hubContext.Clients.Users(Arg.Any<IReadOnlyList<string>>()).Returns(_clients);
        _clients.SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>()).ThrowsAsync(new IOException("backplane down"));

        var act = () => _notifier.NotifyReminderAsync([Guid.NewGuid()], Guid.NewGuid(), TeacherThreadSlaEventKind.FirstReminder, TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
        LoggedLevels().Should().Equal(LogLevel.Warning);
    }

    [Fact]
    public async Task NotifyReplyAsync_RequestAborted_RethrowsWithoutLogging()
    {
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();
        _hubContext.Clients.User(Arg.Any<string>()).Returns(_clients);
        _clients.SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>()).ThrowsAsync(new OperationCanceledException(aborted.Token));

        var act = () => _notifier.NotifyReplyAsync(Guid.NewGuid(), Guid.NewGuid(), aborted.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        LoggedLevels().Should().BeEmpty();
    }

    private List<LogLevel> LoggedLevels() => _logger.ReceivedCalls().Where(x => x.GetMethodInfo().Name == nameof(ILogger.Log)).Select(x => (LogLevel)x.GetArguments()[0]!).ToList();
}
