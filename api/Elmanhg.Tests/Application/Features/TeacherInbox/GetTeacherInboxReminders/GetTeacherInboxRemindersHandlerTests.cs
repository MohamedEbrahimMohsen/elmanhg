using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.TeacherInbox.GetTeacherInboxReminders;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Teachers;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;
using System.Security.Claims;

namespace Elmanhg.Tests.Application.Features.TeacherInbox.GetTeacherInboxReminders;

public sealed class GetTeacherInboxRemindersHandlerTests
{
    private static readonly DateTimeOffset Now = TeacherThreadBuilder.DefaultSubmittedAt.AddHours(21);
    private readonly ITeacherThreadRepository _teacherThreadRepository = Substitute.For<ITeacherThreadRepository>();
    private readonly ITeacherThreadSlaEventRepository _slaEventRepository = Substitute.For<ITeacherThreadSlaEventRepository>();
    private readonly ITeacherSubjectRepository _teacherSubjectRepository = Substitute.For<ITeacherSubjectRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly User _teacher = User.CreateTeacher("Mohamed", "teacher@example.com");
    private readonly Subject _subject = Subject.Create("Physics", 1, Guid.NewGuid());
    private readonly List<TeacherThread> _threads = [];
    private readonly List<TeacherThreadSlaEvent> _events = [];
    private readonly GetTeacherInboxRemindersHandler _handler;

    public GetTeacherInboxRemindersHandlerTests()
    {
        _currentUserService.UserId.Returns(_teacher.Id);
        _currentUserService.GetClaim(ClaimTypes.Role).Returns(nameof(UserRole.Teacher));
        _timeProvider.GetUtcNow().Returns(Now);
        _teacherSubjectRepository.FindAsync(Arg.Any<Expression<Func<TeacherSubject, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<TeacherSubject>, IQueryable<TeacherSubject>>?>(), Arg.Any<Func<IQueryable<TeacherSubject>, IOrderedQueryable<TeacherSubject>>?>(), Arg.Any<bool>())
            .Returns([TeacherSubject.Create(_teacher, _subject, Guid.NewGuid())]);
        _teacherThreadRepository.GetRemindedOpenThreadsAsync(Arg.Any<IReadOnlyCollection<Guid>?>(), Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(_ => _threads.ToList());
        _slaEventRepository.FindAsync(Arg.Any<Expression<Func<TeacherThreadSlaEvent, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<TeacherThreadSlaEvent>, IQueryable<TeacherThreadSlaEvent>>?>(), Arg.Any<Func<IQueryable<TeacherThreadSlaEvent>, IOrderedQueryable<TeacherThreadSlaEvent>>?>(), Arg.Any<bool>())
            .Returns(call => _events.Where(call.Arg<Expression<Func<TeacherThreadSlaEvent, bool>>>().Compile()).ToList());
        _handler = new GetTeacherInboxRemindersHandler(_teacherThreadRepository, _slaEventRepository, _teacherSubjectRepository, Options.Create(new AskTeacherOptions()), _timeProvider, _currentUserService);
    }

    [Fact]
    public async Task Handle_Teacher_PassesAssignedSubjectsAndReturnsLatestReminderKind()
    {
        var bothReminders = Seed(new TeacherThreadBuilder());
        Record(bothReminders, TeacherThreadSlaEventKind.FirstReminder, bothReminders.SlaWindowStartedAt);
        Record(bothReminders, TeacherThreadSlaEventKind.SecondReminder, bothReminders.SlaWindowStartedAt);
        var followedUp = Seed(new TeacherThreadBuilder().AnsweredBy(_teacher.Id).FollowedUp());
        Record(followedUp, TeacherThreadSlaEventKind.SecondReminder, followedUp.SlaWindowStartedAt.AddHours(-3));
        Record(followedUp, TeacherThreadSlaEventKind.FirstReminder, followedUp.SlaWindowStartedAt);

        var result = await _handler.Handle(new GetTeacherInboxRemindersQuery(), TestContext.Current.CancellationToken);

        result.Select(x => (x.ThreadId, x.Kind)).Should().Equal((bothReminders.Id, TeacherThreadSlaEventKind.SecondReminder), (followedUp.Id, TeacherThreadSlaEventKind.FirstReminder));
        await _teacherThreadRepository.Received(1).GetRemindedOpenThreadsAsync(Arg.Is<IReadOnlyCollection<Guid>?>(x => x != null && x.SequenceEqual(new[] { _subject.Id })), _teacher.Id, 20, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Admin_PassesNullSubjects()
    {
        _currentUserService.GetClaim(ClaimTypes.Role).Returns(nameof(UserRole.Admin));

        var result = await _handler.Handle(new GetTeacherInboxRemindersQuery(), TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
        await _teacherThreadRepository.Received(1).GetRemindedOpenThreadsAsync(null, _teacher.Id, 20, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Unauthenticated_ThrowsUnauthorized()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new GetTeacherInboxRemindersQuery(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _teacherThreadRepository.DidNotReceive().GetRemindedOpenThreadsAsync(Arg.Any<IReadOnlyCollection<Guid>?>(), Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    private TeacherThread Seed(TeacherThreadBuilder builder)
    {
        var thread = builder.Build();
        _threads.Add(thread);
        return thread;
    }

    private void Record(TeacherThread thread, TeacherThreadSlaEventKind kind, DateTimeOffset windowStartedAt) => _events.Add(TeacherThreadSlaEvent.Record(thread.Id, kind, windowStartedAt, thread.SlaDueAt, thread.TeacherId, Now));
}
