using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.TeacherInbox.ReplyToTeacherThread;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Domain.Teachers;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;
using System.Security.Claims;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.TeacherInbox.ReplyToTeacherThread;

public sealed class ReplyToTeacherThreadHandlerTests
{
    private const string ReplyText = "Because F = ma.";
    private static readonly DateTimeOffset Now = TeacherThreadBuilder.DefaultSubmittedAt.AddHours(2);
    private readonly ITeacherThreadRepository _teacherThreadRepository = Substitute.For<ITeacherThreadRepository>();
    private readonly ITeacherSubjectRepository _teacherSubjectRepository = Substitute.For<ITeacherSubjectRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly User _teacher = User.CreateTeacher("Mohamed", "teacher@example.com");
    private readonly User _student = User.CreateStudentWithEmail("Ahmed", "student@example.com");
    private readonly List<TeacherThread> _threads = [];
    private readonly ReplyToTeacherThreadHandler _handler;

    public ReplyToTeacherThreadHandlerTests()
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
        _handler = new ReplyToTeacherThreadHandler(_teacherThreadRepository, _teacherSubjectRepository, _userRepository, _timeProvider, _currentUserService);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        await ShouldThrow<UnauthorizedCoreException>(Seed(new TeacherThreadBuilder().ClaimedBy(_teacher.Id)).Id, ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_UnknownThread_ThrowsTeacherThreadNotFound()
    {
        await ShouldThrow<NotFoundCoreException>(Guid.NewGuid(), ErrorCodes.TeacherThreadNotFound);
    }

    [Fact]
    public async Task Handle_TeacherNotAssigned_ThrowsSubjectOutOfScope()
    {
        var thread = Seed(new TeacherThreadBuilder().ClaimedBy(_teacher.Id));
        _teacherSubjectRepository.IsAssignedAsync(_teacher.Id, thread.SubjectId, Arg.Any<CancellationToken>()).Returns(false);

        await ShouldThrow<ForbiddenCoreException>(thread.Id, ErrorCodes.SubjectOutOfScope);
        thread.Messages.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_UnclaimedThread_ThrowsNotClaimed()
    {
        await ShouldThrow<ConflictCoreException>(Seed(new TeacherThreadBuilder()).Id, DomainErrorCodes.TeacherThreadNotClaimed);
    }

    [Fact]
    public async Task Handle_AnsweredThread_ThrowsNotAwaitingReply()
    {
        await ShouldThrow<ConflictCoreException>(Seed(new TeacherThreadBuilder().AnsweredBy(_teacher.Id)).Id, DomainErrorCodes.TeacherThreadNotAwaitingReply);
    }

    [Fact]
    public async Task Handle_ClaimedOpenThread_AppendsReplyAndSaves()
    {
        var thread = Seed(new TeacherThreadBuilder().ClaimedBy(_teacher.Id));

        var result = await _handler.Handle(new ReplyToTeacherThreadCommand(thread.Id, ReplyText), TestContext.Current.CancellationToken);

        (result.Status, result.CanReply, result.CanClaim).Should().Be((TeacherThreadStatus.Answered, false, false));
        var reply = result.Messages.Last();
        (reply.IsFromStudent, reply.Text, reply.CreatedAt).Should().Be((false, ReplyText, Now));
        thread.Status.Should().Be(TeacherThreadStatus.Answered);
        await _teacherThreadRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private TeacherThread Seed(TeacherThreadBuilder builder)
    {
        var thread = builder.ForStudent(_student.Id).Build();
        _threads.Add(thread);
        return thread;
    }

    private async Task ShouldThrow<TException>(Guid threadId, string errorCode) where TException : BaseException
    {
        var act = () => _handler.Handle(new ReplyToTeacherThreadCommand(threadId, ReplyText), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<TException>()).Which.ErrorCode.Should().Be(errorCode);
        await _teacherThreadRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
