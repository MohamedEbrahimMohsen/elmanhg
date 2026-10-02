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
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Diagnostics.Metrics;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.TeacherThreads.ProcessTeacherThreadSla;

public sealed class ProcessTeacherThreadSlaHandlerTests
{
    private static readonly DateTimeOffset SubmittedAt = TeacherThreadBuilder.DefaultSubmittedAt;
    private readonly ITeacherThreadRepository _teacherThreadRepository = Substitute.For<ITeacherThreadRepository>();
    private readonly ITeacherThreadSlaEventRepository _slaEventRepository = Substitute.For<ITeacherThreadSlaEventRepository>();
    private readonly ITeacherSubjectRepository _teacherSubjectRepository = Substitute.For<ITeacherSubjectRepository>();
    private readonly ITeacherThreadNotifier _notifier = Substitute.For<ITeacherThreadNotifier>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly IMeterFactory _meterFactory = MeterFactories.Create();
    private readonly List<TeacherThread> _threads = [];
    private readonly List<TeacherThreadSlaEvent> _recorded = [];
    private readonly List<TeacherThreadSlaEvent> _added = [];
    private readonly List<TeacherSubject> _assignments = [];
    private readonly Subject _subject = Subject.Create("Physics", 1, Guid.NewGuid());
    private readonly Guid _teacherId = Guid.NewGuid();
    private readonly ProcessTeacherThreadSlaHandler _handler;

    public ProcessTeacherThreadSlaHandlerTests()
    {
        _teacherThreadRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<TeacherThread, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<TeacherThread>, IQueryable<TeacherThread>>?>(), Arg.Any<Func<IQueryable<TeacherThread>, IOrderedQueryable<TeacherThread>>?>(), Arg.Any<bool>())
            .Returns(call => _threads.FirstOrDefault(call.Arg<Expression<Func<TeacherThread, bool>>>().Compile()));
        _slaEventRepository.FindAsync(Arg.Any<Expression<Func<TeacherThreadSlaEvent, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<TeacherThreadSlaEvent>, IQueryable<TeacherThreadSlaEvent>>?>(), Arg.Any<Func<IQueryable<TeacherThreadSlaEvent>, IOrderedQueryable<TeacherThreadSlaEvent>>?>(), Arg.Any<bool>())
            .Returns(call => _recorded.Where(call.Arg<Expression<Func<TeacherThreadSlaEvent, bool>>>().Compile()).ToList());
        _slaEventRepository.AddAsync(Arg.Do<TeacherThreadSlaEvent>(_added.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _teacherSubjectRepository.FindAsync(Arg.Any<Expression<Func<TeacherSubject, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<TeacherSubject>, IQueryable<TeacherSubject>>?>(), Arg.Any<Func<IQueryable<TeacherSubject>, IOrderedQueryable<TeacherSubject>>?>(), Arg.Any<bool>())
            .Returns(call => _assignments.Where(call.Arg<Expression<Func<TeacherSubject, bool>>>().Compile()).ToList());
        _handler = new ProcessTeacherThreadSlaHandler(_teacherThreadRepository, _slaEventRepository, _teacherSubjectRepository, _notifier, Substitute.For<ITeacherThreadOutOfAppReminderRepository>(), Substitute.For<IUserRepository>(), Array.Empty<IMessageChannel>(), new ElmanhgMetrics(_meterFactory), new FakeRuntimeSettings(subscriptions: new SubscriptionsOptions { AskTeacherReplySlaHours = 24 }, askTeacher: new AskTeacherOptions()).Set(OutOfAppReminderRuntimeSettings.Enabled, false), _timeProvider, Substitute.For<ILogger<ProcessTeacherThreadSlaHandler>>());
    }

    [Fact]
    public async Task Handle_FirstReminderDueOnClaimedThread_RecordsEventAndNotifiesClaimer()
    {
        var thread = Seed(new TeacherThreadBuilder().ClaimedBy(_teacherId), SubmittedAt.AddHours(13));

        await Handle(thread);

        var recorded = _added.Should().ContainSingle().Subject;
        (recorded.ThreadId, recorded.Kind, recorded.SlaDueAt, recorded.TeacherId, recorded.OccurredAt).Should().Be((thread.Id, TeacherThreadSlaEventKind.FirstReminder, thread.SlaDueAt, (Guid?)_teacherId, SubmittedAt.AddHours(13)));
        await _slaEventRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _notifier.Received(1).NotifyReminderAsync(Arg.Is<IReadOnlyCollection<Guid>>(x => x.SequenceEqual(new[] { _teacherId })), thread.Id, TeacherThreadSlaEventKind.FirstReminder, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FirstReminderDueOnUnclaimedThread_NotifiesAssignedTeachers()
    {
        var first = Assign();
        var second = Assign();
        var thread = Seed(new TeacherThreadBuilder().WithContext(ContextIn(_subject.Id)), SubmittedAt.AddHours(13));

        await Handle(thread);

        await _notifier.Received(1).NotifyReminderAsync(Arg.Is<IReadOnlyCollection<Guid>>(x => x.Order().SequenceEqual(new[] { first, second }.Order())), thread.Id, TeacherThreadSlaEventKind.FirstReminder, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnclaimedThreadWithoutTeachers_RecordsEventWithoutNotifying()
    {
        var thread = Seed(new TeacherThreadBuilder().WithContext(ContextIn(_subject.Id)), SubmittedAt.AddHours(13));

        await Handle(thread);

        _added.Should().ContainSingle();
        await _slaEventRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _notifier.DidNotReceive().NotifyReminderAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<Guid>(), Arg.Any<TeacherThreadSlaEventKind>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OnlyBreachMissing_RecordsBreachCountsMetricAndDoesNotNotify()
    {
        using var events = new MetricCollector<long>(_meterFactory, ElmanhgTelemetry.SourceName, "elmanhg.ask_teacher.sla_events");
        var thread = Seed(new TeacherThreadBuilder().ClaimedBy(_teacherId), SubmittedAt.AddHours(25));
        Record(thread, TeacherThreadSlaEventKind.FirstReminder);
        Record(thread, TeacherThreadSlaEventKind.SecondReminder);

        await Handle(thread);

        _added.Should().ContainSingle().Which.Kind.Should().Be(TeacherThreadSlaEventKind.Breach);
        var measurement = events.GetMeasurementSnapshot().Should().ContainSingle().Subject;
        (measurement.Value, measurement.Tags[ElmanhgMetrics.KindTag]).Should().Be((1L, (object?)"Breach"));
        await _slaEventRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _notifier.DidNotReceive().NotifyReminderAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<Guid>(), Arg.Any<TeacherThreadSlaEventKind>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AllStagesMissing_RecordsThreeEventsAndNotifiesSecondReminderOnce()
    {
        var thread = Seed(new TeacherThreadBuilder().ClaimedBy(_teacherId), SubmittedAt.AddHours(25));

        await Handle(thread);

        _added.Select(x => x.Kind).Should().Equal(TeacherThreadSlaEventKind.FirstReminder, TeacherThreadSlaEventKind.SecondReminder, TeacherThreadSlaEventKind.Breach);
        await _slaEventRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _notifier.Received(1).NotifyReminderAsync(Arg.Any<IReadOnlyCollection<Guid>>(), thread.Id, TeacherThreadSlaEventKind.SecondReminder, Arg.Any<CancellationToken>());
        await _notifier.Received(1).NotifyReminderAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<Guid>(), Arg.Any<TeacherThreadSlaEventKind>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_StagesAlreadyRecorded_DoesNothing()
    {
        var thread = Seed(new TeacherThreadBuilder().ClaimedBy(_teacherId), SubmittedAt.AddHours(13));
        Record(thread, TeacherThreadSlaEventKind.FirstReminder);

        await Handle(thread);

        await ShouldHaveDoneNothing();
    }

    [Fact]
    public async Task Handle_AnsweredThread_DoesNothing()
    {
        var thread = Seed(new TeacherThreadBuilder().AnsweredBy(_teacherId), SubmittedAt.AddHours(30));

        await Handle(thread);

        await ShouldHaveDoneNothing();
    }

    [Fact]
    public async Task Handle_ThreadMissing_DoesNothing()
    {
        _timeProvider.GetUtcNow().Returns(SubmittedAt.AddHours(30));

        await _handler.Handle(new ProcessTeacherThreadSlaCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        await ShouldHaveDoneNothing();
    }

    private TeacherThread Seed(TeacherThreadBuilder builder, DateTimeOffset now)
    {
        var thread = builder.Build();
        _threads.Add(thread);
        _timeProvider.GetUtcNow().Returns(now);
        return thread;
    }

    private void Record(TeacherThread thread, TeacherThreadSlaEventKind kind) => _recorded.Add(TeacherThreadSlaEvent.Record(thread.Id, kind, thread.SlaDueAt, thread.TeacherId, SubmittedAt.AddHours(12)));

    private Guid Assign()
    {
        var teacher = User.CreateTeacher("Teacher", $"{Guid.NewGuid():N}@example.com");
        _assignments.Add(TeacherSubject.Create(teacher, _subject, Guid.NewGuid()));
        return teacher.Id;
    }

    private static TeacherThreadContext ContextIn(Guid subjectId) => new(subjectId, "Physics", Guid.NewGuid(), "Mechanics", Guid.NewGuid(), "Newton's laws", null, null, null, null);

    private Task Handle(TeacherThread thread) => _handler.Handle(new ProcessTeacherThreadSlaCommand(thread.Id), TestContext.Current.CancellationToken);

    private async Task ShouldHaveDoneNothing()
    {
        _added.Should().BeEmpty();
        await _slaEventRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _notifier.DidNotReceive().NotifyReminderAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<Guid>(), Arg.Any<TeacherThreadSlaEventKind>(), Arg.Any<CancellationToken>());
    }
}
