using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.TeacherInbox.ClaimTeacherThread;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Domain.Teachers;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;
using System.Security.Claims;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.TeacherInbox.ClaimTeacherThread;

public sealed class ClaimTeacherThreadHandlerTests
{
    private static readonly DateTimeOffset Now = TeacherThreadBuilder.DefaultSubmittedAt.AddHours(1);
    private readonly ITeacherThreadRepository _teacherThreadRepository = Substitute.For<ITeacherThreadRepository>();
    private readonly ITeacherSubjectRepository _teacherSubjectRepository = Substitute.For<ITeacherSubjectRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly User _teacher = User.CreateTeacher("Mohamed", "teacher@example.com");
    private readonly User _student = User.CreateStudentWithEmail("Ahmed", "student@example.com");
    private readonly List<TeacherThread> _threads = [];
    private readonly ClaimTeacherThreadHandler _handler;

    public ClaimTeacherThreadHandlerTests()
    {
        User[] users = [_teacher, _student];
        _currentUserService.UserId.Returns(_teacher.Id);
        _currentUserService.GetClaim(ClaimTypes.Role).Returns(nameof(UserRole.Teacher));
        _timeProvider.GetUtcNow().Returns(Now);
        _teacherSubjectRepository.IsAssignedAsync(_teacher.Id, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        _teacherThreadRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<TeacherThread, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<TeacherThread>, IQueryable<TeacherThread>>?>(), Arg.Any<Func<IQueryable<TeacherThread>, IOrderedQueryable<TeacherThread>>?>(), Arg.Any<bool>())
            .Returns(call => _threads.FirstOrDefault(call.Arg<Expression<Func<TeacherThread, bool>>>().Compile()));
        _userRepository.FindAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<User>, IQueryable<User>>?>(), Arg.Any<Func<IQueryable<User>, IOrderedQueryable<User>>?>(), Arg.Any<bool>())
            .Returns(call => users.Where(call.Arg<Expression<Func<User, bool>>>().Compile()).ToList());
        _handler = new ClaimTeacherThreadHandler(_teacherThreadRepository, _teacherSubjectRepository, _userRepository, _timeProvider, _currentUserService);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new ClaimTeacherThreadCommand(Seed(new TeacherThreadBuilder()).Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _teacherThreadRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownThread_ThrowsTeacherThreadNotFound()
    {
        var act = () => _handler.Handle(new ClaimTeacherThreadCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.TeacherThreadNotFound);
        await _teacherThreadRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TeacherNotAssigned_ThrowsSubjectOutOfScope()
    {
        var thread = Seed(new TeacherThreadBuilder());
        _teacherSubjectRepository.IsAssignedAsync(_teacher.Id, thread.SubjectId, Arg.Any<CancellationToken>()).Returns(false);

        var act = () => _handler.Handle(new ClaimTeacherThreadCommand(thread.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ForbiddenCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.SubjectOutOfScope);
        thread.TeacherId.Should().BeNull();
        await _teacherThreadRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ClaimedByAnother_ThrowsAlreadyClaimed()
    {
        var thread = Seed(new TeacherThreadBuilder().ClaimedBy(Guid.NewGuid()));

        var act = () => _handler.Handle(new ClaimTeacherThreadCommand(thread.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(DomainErrorCodes.TeacherThreadAlreadyClaimed);
        await _teacherThreadRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnclaimedThread_ClaimsAndSaves()
    {
        var thread = Seed(new TeacherThreadBuilder());

        var result = await _handler.Handle(new ClaimTeacherThreadCommand(thread.Id), TestContext.Current.CancellationToken);

        (result.IsClaimedByMe, result.CanReply, result.CanClaim, result.TeacherName, result.StudentName).Should().Be((true, true, false, "Mohamed", "Ahmed"));
        (thread.TeacherId, thread.ClaimedAt).Should().Be(((Guid?)_teacher.Id, (DateTimeOffset?)Now));
        await _teacherThreadRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private TeacherThread Seed(TeacherThreadBuilder builder)
    {
        var thread = builder.ForStudent(_student.Id).Build();
        _threads.Add(thread);
        return thread;
    }
}
