using Core.DDD.Models;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.TeacherInbox.GetTeacherInbox;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Domain.Teachers;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;
using System.Security.Claims;

namespace Elmanhg.Tests.Application.Features.TeacherInbox.GetTeacherInbox;

public sealed class GetTeacherInboxHandlerTests
{
    private static readonly DateTimeOffset Now = TeacherThreadBuilder.DefaultSubmittedAt.AddHours(1);
    private readonly ITeacherThreadRepository _teacherThreadRepository = Substitute.For<ITeacherThreadRepository>();
    private readonly ITeacherSubjectRepository _teacherSubjectRepository = Substitute.For<ITeacherSubjectRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly User _teacher = User.CreateTeacher("Mohamed", "teacher@example.com");
    private readonly User _student = User.CreateStudentWithEmail("Ahmed", "student@example.com");
    private readonly Subject _subject = Subject.Create("Physics", 1, Guid.NewGuid());
    private readonly GetTeacherInboxHandler _handler;
    private Expression<Func<TeacherThread, bool>>? _filter;

    public GetTeacherInboxHandlerTests()
    {
        User[] users = [_teacher, _student];
        _currentUserService.UserId.Returns(_teacher.Id);
        _currentUserService.GetClaim(ClaimTypes.Role).Returns(nameof(UserRole.Teacher));
        _timeProvider.GetUtcNow().Returns(Now);
        _teacherSubjectRepository.FindAsync(Arg.Any<Expression<Func<TeacherSubject, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<TeacherSubject>, IQueryable<TeacherSubject>>?>(), Arg.Any<Func<IQueryable<TeacherSubject>, IOrderedQueryable<TeacherSubject>>?>(), Arg.Any<bool>())
            .Returns([TeacherSubject.Create(_teacher, _subject, Guid.NewGuid())]);
        _userRepository.FindAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<User>, IQueryable<User>>?>(), Arg.Any<Func<IQueryable<User>, IOrderedQueryable<User>>?>(), Arg.Any<bool>())
            .Returns(call => users.Where(call.Arg<Expression<Func<User, bool>>>().Compile()).ToList());
        _handler = new GetTeacherInboxHandler(_teacherThreadRepository, _teacherSubjectRepository, _userRepository, _timeProvider, _currentUserService);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new GetTeacherInboxQuery(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_Teacher_PassesAssignedSubjectsToFilterAndMapsItems()
    {
        var thread = ThreadIn(_subject.Id);
        ArrangePage(thread);

        var result = await _handler.Handle(new GetTeacherInboxQuery(), TestContext.Current.CancellationToken);

        var predicate = _filter!.Compile();
        predicate(thread).Should().BeTrue();
        predicate(ThreadIn(Guid.NewGuid())).Should().BeFalse();
        var item = result.Items.Should().ContainSingle().Subject;
        (item.Id, item.StudentName, item.TeacherName, item.IsClaimedByMe, item.QuestionText).Should().Be((thread.Id, "Ahmed", (string?)null, false, "Why is F = ma?"));
        (result.PageNumber, result.PageSize, result.TotalItems, result.TotalPages).Should().Be((1, 20, 1, 1));
    }

    [Fact]
    public async Task Handle_Admin_DoesNotReadAssignments()
    {
        _currentUserService.GetClaim(ClaimTypes.Role).Returns(nameof(UserRole.Admin));
        ArrangePage();

        await _handler.Handle(new GetTeacherInboxQuery(), TestContext.Current.CancellationToken);

        await _teacherSubjectRepository.DidNotReceive().FindAsync(Arg.Any<Expression<Func<TeacherSubject, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<TeacherSubject>, IQueryable<TeacherSubject>>?>(), Arg.Any<Func<IQueryable<TeacherSubject>, IOrderedQueryable<TeacherSubject>>?>(), Arg.Any<bool>());
        _filter!.Compile()(ThreadIn(Guid.NewGuid())).Should().BeTrue();
    }

    private TeacherThread ThreadIn(Guid subjectId) => new TeacherThreadBuilder().ForStudent(_student.Id).WithContext(new TeacherThreadContext(subjectId, "Physics", Guid.NewGuid(), "Mechanics", Guid.NewGuid(), "Newton's laws", null, null, null, null)).Build();

    private void ArrangePage(params TeacherThread[] threads)
    {
        _teacherThreadRepository.FindPaginatedAsync(1, 20, Arg.Any<CancellationToken>(), Arg.Do<Expression<Func<TeacherThread, bool>>?>(x => _filter = x), Arg.Any<Func<IQueryable<TeacherThread>, IQueryable<TeacherThread>>?>(), Arg.Any<Func<IQueryable<TeacherThread>, IOrderedQueryable<TeacherThread>>?>(), Arg.Any<bool>())
            .Returns(new PageData<TeacherThread> { Items = [.. threads], PageNumber = 1, PageSize = 20, TotalItems = threads.Length, TotalPages = 1 });
    }
}
