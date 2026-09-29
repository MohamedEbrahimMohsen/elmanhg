using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.TeacherInbox.GetInboxThread;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Domain.Teachers;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;
using System.Security.Claims;

namespace Elmanhg.Tests.Application.Features.TeacherInbox.GetInboxThread;

public sealed class GetInboxThreadHandlerTests
{
    private static readonly DateTimeOffset Now = TeacherThreadBuilder.DefaultSubmittedAt.AddHours(1);
    private readonly ITeacherThreadRepository _teacherThreadRepository = Substitute.For<ITeacherThreadRepository>();
    private readonly ITeacherSubjectRepository _teacherSubjectRepository = Substitute.For<ITeacherSubjectRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly User _teacher = User.CreateTeacher("Mohamed", "teacher@example.com");
    private readonly User _student = User.CreateStudentWithEmail("Ahmed", "student@example.com");
    private readonly TeacherThread _thread;
    private readonly GetInboxThreadHandler _handler;

    public GetInboxThreadHandlerTests()
    {
        _thread = new TeacherThreadBuilder().ForStudent(_student.Id).Build();
        TeacherThread[] threads = [_thread];
        User[] users = [_teacher, _student];
        _currentUserService.UserId.Returns(_teacher.Id);
        _currentUserService.GetClaim(ClaimTypes.Role).Returns(nameof(UserRole.Teacher));
        _timeProvider.GetUtcNow().Returns(Now);
        _teacherSubjectRepository.IsAssignedAsync(_teacher.Id, _thread.SubjectId, Arg.Any<CancellationToken>()).Returns(true);
        _teacherThreadRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<TeacherThread, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<TeacherThread>, IQueryable<TeacherThread>>?>(), Arg.Any<Func<IQueryable<TeacherThread>, IOrderedQueryable<TeacherThread>>?>(), Arg.Any<bool>())
            .Returns(call => threads.FirstOrDefault(call.Arg<Expression<Func<TeacherThread, bool>>>().Compile()));
        _userRepository.FindAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<User>, IQueryable<User>>?>(), Arg.Any<Func<IQueryable<User>, IOrderedQueryable<User>>?>(), Arg.Any<bool>())
            .Returns(call => users.Where(call.Arg<Expression<Func<User, bool>>>().Compile()).ToList());
        _handler = new GetInboxThreadHandler(_teacherThreadRepository, _teacherSubjectRepository, _userRepository, _timeProvider, _currentUserService);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new GetInboxThreadQuery(_thread.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_UnknownThread_ThrowsTeacherThreadNotFound()
    {
        var act = () => _handler.Handle(new GetInboxThreadQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.TeacherThreadNotFound);
    }

    [Fact]
    public async Task Handle_TeacherNotAssigned_ThrowsSubjectOutOfScope()
    {
        _teacherSubjectRepository.IsAssignedAsync(_teacher.Id, _thread.SubjectId, Arg.Any<CancellationToken>()).Returns(false);

        var act = () => _handler.Handle(new GetInboxThreadQuery(_thread.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ForbiddenCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.SubjectOutOfScope);
    }

    [Fact]
    public async Task Handle_AssignedTeacher_ReturnsThreadWithNames()
    {
        var result = await _handler.Handle(new GetInboxThreadQuery(_thread.Id), TestContext.Current.CancellationToken);

        (result.Id, result.StudentName, result.TeacherName, result.CanClaim, result.CanReply, result.IsOverdue).Should().Be((_thread.Id, "Ahmed", (string?)null, true, false, false));
        (result.Context.LessonName, result.SlaDueAt).Should().Be(("Newton's laws", _thread.SlaDueAt));
        result.Messages.Should().ContainSingle().Which.Text.Should().Be("Why is F = ma?");
    }

    [Fact]
    public async Task Handle_Admin_SkipsAssignmentCheck()
    {
        _currentUserService.GetClaim(ClaimTypes.Role).Returns(nameof(UserRole.Admin));

        var result = await _handler.Handle(new GetInboxThreadQuery(_thread.Id), TestContext.Current.CancellationToken);

        result.Id.Should().Be(_thread.Id);
        await _teacherSubjectRepository.DidNotReceive().IsAssignedAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
