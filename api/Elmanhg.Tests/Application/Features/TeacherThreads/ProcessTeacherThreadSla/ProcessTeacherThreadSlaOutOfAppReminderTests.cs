using Elmanhg.Application.Shared.Messaging;
using Elmanhg.Application.Shared.Observability;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.Realtime;
using Elmanhg.Application.Shared.RuntimeSettings.Definitions;
using Elmanhg.Application.TeacherThreads.ProcessTeacherThreadSla;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Teachers;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Tests.Application.Features.Shared.Observability;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Fixtures.RuntimeSettings;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.TeacherThreads.ProcessTeacherThreadSla;

public sealed class ProcessTeacherThreadSlaOutOfAppReminderTests
{
    private const string ClaimerPhone = "01012345678";
    private static readonly DateTimeOffset SubmittedAt = TeacherThreadBuilder.DefaultSubmittedAt;
    private static readonly DateTimeOffset SecondReminderDueAt = SubmittedAt.AddHours(21);
    private readonly ITeacherThreadRepository _teacherThreadRepository = Substitute.For<ITeacherThreadRepository>();
    private readonly ITeacherThreadSlaEventRepository _slaEventRepository = Substitute.For<ITeacherThreadSlaEventRepository>();
    private readonly ITeacherSubjectRepository _teacherSubjectRepository = Substitute.For<ITeacherSubjectRepository>();
    private readonly ITeacherThreadOutOfAppReminderRepository _reminderRepository = Substitute.For<ITeacherThreadOutOfAppReminderRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ITeacherThreadNotifier _notifier = Substitute.For<ITeacherThreadNotifier>();
    private readonly IMessageChannel _whatsApp = Substitute.For<IMessageChannel>();
    private readonly IMessageChannel _email = Substitute.For<IMessageChannel>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly FakeLogger<ProcessTeacherThreadSlaHandler> _logger = new();
    private readonly FakeRuntimeSettings _settings = new(subscriptions: new SubscriptionsOptions { AskTeacherReplySlaHours = 24 }, askTeacher: new AskTeacherOptions());
    private readonly List<TeacherThread> _threads = [];
    private readonly List<TeacherThreadSlaEvent> _recorded = [];
    private readonly List<TeacherThreadOutOfAppReminder> _markers = [];
    private readonly List<TeacherSubject> _assignments = [];
    private readonly List<User> _users = [];
    private readonly Subject _subject = Subject.Create("Physics", 1, Guid.NewGuid());

    public ProcessTeacherThreadSlaOutOfAppReminderTests()
    {
        _teacherThreadRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<TeacherThread, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<TeacherThread>, IQueryable<TeacherThread>>?>(), Arg.Any<Func<IQueryable<TeacherThread>, IOrderedQueryable<TeacherThread>>?>(), Arg.Any<bool>())
            .Returns(call => _threads.FirstOrDefault(call.Arg<Expression<Func<TeacherThread, bool>>>().Compile()));
        _slaEventRepository.FindAsync(Arg.Any<Expression<Func<TeacherThreadSlaEvent, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<TeacherThreadSlaEvent>, IQueryable<TeacherThreadSlaEvent>>?>(), Arg.Any<Func<IQueryable<TeacherThreadSlaEvent>, IOrderedQueryable<TeacherThreadSlaEvent>>?>(), Arg.Any<bool>())
            .Returns(call => _recorded.Where(call.Arg<Expression<Func<TeacherThreadSlaEvent, bool>>>().Compile()).ToList());
        _teacherSubjectRepository.FindAsync(Arg.Any<Expression<Func<TeacherSubject, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<TeacherSubject>, IQueryable<TeacherSubject>>?>(), Arg.Any<Func<IQueryable<TeacherSubject>, IOrderedQueryable<TeacherSubject>>?>(), Arg.Any<bool>())
            .Returns(call => _assignments.Where(call.Arg<Expression<Func<TeacherSubject, bool>>>().Compile()).ToList());
        _userRepository.FindAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<User>, IQueryable<User>>?>(), Arg.Any<Func<IQueryable<User>, IOrderedQueryable<User>>?>(), Arg.Any<bool>())
            .Returns(call => _users.Where(call.Arg<Expression<Func<User, bool>>>().Compile()).ToList());
        _reminderRepository.AddAsync(Arg.Do<TeacherThreadOutOfAppReminder>(_markers.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _whatsApp.Channel.Returns(MessageChannel.WhatsApp);
        _email.Channel.Returns(MessageChannel.Email);
        _whatsApp.SendAsync(Arg.Any<OutboundMessage>(), Arg.Any<CancellationToken>()).Returns(true);
        _email.SendAsync(Arg.Any<OutboundMessage>(), Arg.Any<CancellationToken>()).Returns(true);
    }

    [Fact]
    public async Task Handle_ConfiguredStageNewlyDue_RecordsMarkerInTheSameSave()
    {
        var claimer = Teacher(ClaimerPhone);
        var thread = Seed(new TeacherThreadBuilder().ClaimedBy(claimer.Id), SecondReminderDueAt);

        await Handle(thread);

        var marker = _markers.Should().ContainSingle().Subject;
        (marker.ThreadId, marker.Stage, marker.SlaDueAt, marker.OccurredAt).Should().Be((thread.Id, TeacherThreadSlaEventKind.SecondReminder, thread.SlaDueAt, SecondReminderDueAt));
        await _slaEventRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        Received.InOrder(async () =>
        {
            await _reminderRepository.AddAsync(Arg.Any<TeacherThreadOutOfAppReminder>(), Arg.Any<CancellationToken>());
            await _slaEventRepository.SaveChangesAsync(Arg.Any<CancellationToken>());
            await _whatsApp.SendAsync(Arg.Any<OutboundMessage>(), Arg.Any<CancellationToken>());
            await _email.SendAsync(Arg.Any<OutboundMessage>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Handle_ClaimedThread_SendsOnlyToClaimer()
    {
        var claimer = Teacher(ClaimerPhone);
        Assign(Teacher("01112345678"));
        Assign(Teacher("01212345678"));
        var thread = Seed(new TeacherThreadBuilder().ClaimedBy(claimer.Id).WithContext(ContextIn(_subject.Id)), SecondReminderDueAt);

        await Handle(thread);

        var whatsApp = SentOver(_whatsApp).Should().ContainSingle().Subject;
        var email = SentOver(_email).Should().ContainSingle().Subject;
        whatsApp.Should().Be(new TeacherThreadReminderMessage(claimer.Id, ClaimerPhone, claimer.DisplayName, thread.Id, "Physics", "Newton's laws", thread.SlaDueAt));
        email.Should().Be(whatsApp with { Address = claimer.Email! });
    }

    [Fact]
    public async Task Handle_UnclaimedThread_SendsToEveryAssignedTeacher()
    {
        var first = Assign(Teacher("01112345678"));
        var second = Assign(Teacher("01212345678"));
        var thread = Seed(new TeacherThreadBuilder().WithContext(ContextIn(_subject.Id)), SecondReminderDueAt);

        await Handle(thread);

        SentOver(_whatsApp).Select(x => x.RecipientUserId).Should().BeEquivalentTo(new[] { first.Id, second.Id });
        SentOver(_email).Select(x => x.RecipientUserId).Should().BeEquivalentTo(new[] { first.Id, second.Id });
    }

    [Fact]
    public async Task Handle_MarkerAlreadyRecorded_DoesNotRecordOrSendAgain()
    {
        var claimer = Teacher(ClaimerPhone);
        var thread = Seed(new TeacherThreadBuilder().ClaimedBy(claimer.Id), SecondReminderDueAt);
        _reminderRepository.IsRecordedAsync(thread.Id, Arg.Any<CancellationToken>()).Returns(true);

        await Handle(thread);

        _markers.Should().BeEmpty();
        await ShouldNotHaveSent();
        await _notifier.Received(1).NotifyReminderAsync(Arg.Any<IReadOnlyCollection<Guid>>(), thread.Id, TeacherThreadSlaEventKind.SecondReminder, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_StageAlreadyRecorded_DoesNotSend()
    {
        var claimer = Teacher(ClaimerPhone);
        var thread = Seed(new TeacherThreadBuilder().ClaimedBy(claimer.Id), SecondReminderDueAt);
        _recorded.Add(TeacherThreadSlaEvent.Record(thread.Id, TeacherThreadSlaEventKind.SecondReminder, thread.SlaWindowStartedAt, thread.SlaDueAt, thread.TeacherId, SubmittedAt.AddHours(20)));

        await Handle(thread);

        _markers.Should().BeEmpty();
        await ShouldNotHaveSent();
        await _reminderRepository.DidNotReceive().IsRecordedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OtherStageDue_DoesNotRecordOrSend()
    {
        var claimer = Teacher(ClaimerPhone);
        var thread = Seed(new TeacherThreadBuilder().ClaimedBy(claimer.Id), SubmittedAt.AddHours(13));

        await Handle(thread);

        _markers.Should().BeEmpty();
        await ShouldNotHaveSent();
    }

    [Fact]
    public async Task Handle_FirstReminderConfigured_SendsAtFirstReminder()
    {
        _settings.Set(OutOfAppReminderRuntimeSettings.Stage, nameof(TeacherThreadSlaEventKind.FirstReminder));
        var claimer = Teacher(ClaimerPhone);
        var thread = Seed(new TeacherThreadBuilder().ClaimedBy(claimer.Id), SubmittedAt.AddHours(13));

        await Handle(thread);

        _markers.Should().ContainSingle().Which.Stage.Should().Be(TeacherThreadSlaEventKind.FirstReminder);
        await _whatsApp.Received(1).SendAsync(Arg.Any<OutboundMessage>(), Arg.Any<CancellationToken>());
        await _email.Received(1).SendAsync(Arg.Any<OutboundMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Disabled_DoesNotRecordOrSend()
    {
        _settings.Set(OutOfAppReminderRuntimeSettings.Enabled, false);
        var claimer = Teacher(ClaimerPhone);
        var thread = Seed(new TeacherThreadBuilder().ClaimedBy(claimer.Id), SecondReminderDueAt);

        await Handle(thread);

        _markers.Should().BeEmpty();
        await ShouldNotHaveSent();
        await _notifier.Received(1).NotifyReminderAsync(Arg.Any<IReadOnlyCollection<Guid>>(), thread.Id, TeacherThreadSlaEventKind.SecondReminder, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EmailOnlyChannel_SendsEmailOnly()
    {
        _settings.Set(OutOfAppReminderRuntimeSettings.Channels, nameof(MessageChannel.Email));
        var claimer = Teacher(ClaimerPhone);
        var thread = Seed(new TeacherThreadBuilder().ClaimedBy(claimer.Id), SecondReminderDueAt);

        await Handle(thread);

        await _whatsApp.DidNotReceive().SendAsync(Arg.Any<OutboundMessage>(), Arg.Any<CancellationToken>());
        await _email.Received(1).SendAsync(Arg.Any<OutboundMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhatsAppFails_StillSendsEmailAndCompletes()
    {
        _whatsApp.SendAsync(Arg.Any<OutboundMessage>(), Arg.Any<CancellationToken>()).Returns(false);
        var claimer = Teacher(ClaimerPhone);
        var thread = Seed(new TeacherThreadBuilder().ClaimedBy(claimer.Id), SecondReminderDueAt);

        var act = () => Handle(thread);

        await act.Should().NotThrowAsync();
        await _email.Received(1).SendAsync(Arg.Any<OutboundMessage>(), Arg.Any<CancellationToken>());
        var failure = _logger.Collector.GetSnapshot().Should().ContainSingle(x => x.Level == LogLevel.Warning).Subject;
        (failure.StructuredState!.Single(x => x.Key == "Channel").Value, failure.StructuredState!.Single(x => x.Key == "Outcome").Value).Should().Be(("WhatsApp", "Failed"));
    }

    [Fact]
    public async Task Handle_EmailFails_StillSendsWhatsApp()
    {
        _email.SendAsync(Arg.Any<OutboundMessage>(), Arg.Any<CancellationToken>()).Returns(false);
        var claimer = Teacher(ClaimerPhone);
        var thread = Seed(new TeacherThreadBuilder().ClaimedBy(claimer.Id), SecondReminderDueAt);

        var act = () => Handle(thread);

        await act.Should().NotThrowAsync();
        await _whatsApp.Received(1).SendAsync(Arg.Any<OutboundMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TeacherWithoutPhone_SkipsWhatsAppAndSendsEmail()
    {
        var claimer = Teacher(phoneNumber: null);
        var thread = Seed(new TeacherThreadBuilder().ClaimedBy(claimer.Id), SecondReminderDueAt);

        await Handle(thread);

        await _whatsApp.DidNotReceive().SendAsync(Arg.Any<OutboundMessage>(), Arg.Any<CancellationToken>());
        await _email.Received(1).SendAsync(Arg.Any<OutboundMessage>(), Arg.Any<CancellationToken>());
        _logger.Collector.GetSnapshot().Should().ContainSingle(x => x.Level == LogLevel.Information && x.Message.Contains("no contact on file") && x.Message.Contains(claimer.Id.ToString()));
    }

    [Fact]
    public async Task Handle_SuspendedOrPendingTeacher_IsNotMessaged()
    {
        var active = Assign(Teacher(ClaimerPhone));
        var suspended = Assign(Teacher("01112345678"));
        suspended.Suspend();
        var pending = Assign(Teacher("01212345678"));
        pending.PasswordHash = null;
        var thread = Seed(new TeacherThreadBuilder().WithContext(ContextIn(_subject.Id)), SecondReminderDueAt);

        await Handle(thread);

        SentOver(_whatsApp).Concat(SentOver(_email)).Select(x => x.RecipientUserId).Should().OnlyContain(x => x == active.Id).And.HaveCount(2);
    }

    [Fact]
    public async Task Handle_NoRecipients_RecordsMarkerWithoutSending()
    {
        var thread = Seed(new TeacherThreadBuilder().WithContext(ContextIn(_subject.Id)), SecondReminderDueAt);

        await Handle(thread);

        _markers.Should().ContainSingle();
        await _slaEventRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await ShouldNotHaveSent();
    }

    [Fact]
    public async Task Handle_AllStagesMissing_SendsOneReminderPerChannel()
    {
        var claimer = Teacher(ClaimerPhone);
        var thread = Seed(new TeacherThreadBuilder().ClaimedBy(claimer.Id), SubmittedAt.AddHours(25));

        await Handle(thread);

        _markers.Should().ContainSingle();
        await _whatsApp.Received(1).SendAsync(Arg.Any<OutboundMessage>(), Arg.Any<CancellationToken>());
        await _email.Received(1).SendAsync(Arg.Any<OutboundMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Sends_LogsWithoutContactDetails()
    {
        var claimer = Teacher(ClaimerPhone);
        var thread = Seed(new TeacherThreadBuilder().ClaimedBy(claimer.Id), SecondReminderDueAt);

        await Handle(thread);

        var messages = _logger.Collector.GetSnapshot().Select(x => x.Message).ToList();
        messages.Should().HaveCount(2).And.OnlyContain(x => x.Contains(thread.Id.ToString()));
        messages.Should().NotContain(x => x.Contains(ClaimerPhone) || x.Contains(claimer.Email!));
    }

    [Fact]
    public async Task Handle_CalendarSkipsWeekend_SendsAtCalendarStageWithCalendarDeadline()
    {
        var claimer = Teacher(ClaimerPhone);
        var thread = Seed(new TeacherThreadBuilder().ClaimedBy(claimer.Id).WithSlaPolicy(TeacherThreadSlaPolicies.CairoWeekends()), new DateTimeOffset(2026, 10, 4, 8, 0, 0, TimeSpan.Zero));

        await Handle(thread);

        _markers.Should().ContainSingle().Which.Stage.Should().Be(TeacherThreadSlaEventKind.SecondReminder);
        SentOver(_whatsApp).Should().ContainSingle().Which.SlaDueAt.Should().Be(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task Handle_CalendarSkipsWeekendAtWallClockStage_DoesNotSend()
    {
        var claimer = Teacher(ClaimerPhone);
        var thread = Seed(new TeacherThreadBuilder().ClaimedBy(claimer.Id).WithSlaPolicy(TeacherThreadSlaPolicies.CairoWeekends()), SecondReminderDueAt);

        await Handle(thread);

        _markers.Should().BeEmpty();
        await ShouldNotHaveSent();
    }

    private User Teacher(string? phoneNumber)
    {
        var teacher = User.CreateTeacher("Teacher", $"{Guid.NewGuid():N}@example.com");
        teacher.PasswordHash = "not-a-secret-password-hash";
        teacher.SetContactPhoneNumber(phoneNumber, Guid.NewGuid());
        _users.Add(teacher);
        return teacher;
    }

    private User Assign(User teacher)
    {
        _assignments.Add(TeacherSubject.Create(teacher, _subject, Guid.NewGuid()));
        return teacher;
    }

    private TeacherThread Seed(TeacherThreadBuilder builder, DateTimeOffset now)
    {
        var thread = builder.Build();
        _threads.Add(thread);
        _timeProvider.GetUtcNow().Returns(now);
        return thread;
    }

    private static TeacherThreadContext ContextIn(Guid subjectId) => new(subjectId, "Physics", Guid.NewGuid(), "Mechanics", Guid.NewGuid(), "Newton's laws", null, null, null, null);

    private static List<TeacherThreadReminderMessage> SentOver(IMessageChannel channel) => channel.ReceivedCalls()
        .Where(x => x.GetMethodInfo().Name == nameof(IMessageChannel.SendAsync))
        .Select(x => (TeacherThreadReminderMessage)x.GetArguments()[0]!)
        .ToList();

    private async Task ShouldNotHaveSent()
    {
        await _whatsApp.DidNotReceive().SendAsync(Arg.Any<OutboundMessage>(), Arg.Any<CancellationToken>());
        await _email.DidNotReceive().SendAsync(Arg.Any<OutboundMessage>(), Arg.Any<CancellationToken>());
    }

    private Task Handle(TeacherThread thread) => new ProcessTeacherThreadSlaHandler(_teacherThreadRepository, _slaEventRepository, _teacherSubjectRepository, _notifier, _reminderRepository, _userRepository, [_whatsApp, _email], new ElmanhgMetrics(MeterFactories.Create()), _settings, _timeProvider, _logger).Handle(new ProcessTeacherThreadSlaCommand(thread.Id), TestContext.Current.CancellationToken);
}
