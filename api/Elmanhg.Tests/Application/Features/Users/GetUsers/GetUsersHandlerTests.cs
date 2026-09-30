using Core.DDD.Models;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Users.GetUsers;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Domain.Teachers;
using Elmanhg.Tests.Application.Features.Subscriptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Users.GetUsers;

public sealed class GetUsersHandlerTests
{
    private static readonly DateTimeOffset Now = SubscriptionBuilder.DefaultStart.AddDays(10);

    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ISubscriptionRepository _subscriptionRepository = Substitute.For<ISubscriptionRepository>();
    private readonly ITeacherSubjectRepository _teacherSubjectRepository = Substitute.For<ITeacherSubjectRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly User _admin = User.CreateAdmin("Admin", "admin@elmanhg.test");
    private readonly GetUsersHandler _handler;

    public GetUsersHandlerTests()
    {
        _currentUserService.UserId.Returns(_admin.Id);
        _timeProvider.GetUtcNow().Returns(Now);
        SubscriptionRepositoryStub.Stub(_subscriptionRepository);
        _handler = new GetUsersHandler(_userRepository, _subscriptionRepository, _teacherSubjectRepository, _currentUserService, Options.Create(new SubscriptionsOptions { GracePeriodDays = 3 }), _timeProvider);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new GetUsersQuery(UserRole.Student, null, null), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _userRepository.DidNotReceive().FindPaginatedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<User, bool>>?>(), Arg.Any<Func<IQueryable<User>, IQueryable<User>>?>(), Arg.Any<Func<IQueryable<User>, IOrderedQueryable<User>>?>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task Handle_Students_ReturnsMaskedContactAndEntitledTier()
    {
        var student = User.CreateStudentWithPhone("Mona", "01012345678");
        SubscriptionRepositoryStub.Stub(_subscriptionRepository, SubscriptionRepositoryStub.EntitledBase(student.Id, Now), SubscriptionRepositoryStub.EntitledBase(Guid.NewGuid(), Now));
        StubPage(student);

        var result = await _handler.Handle(new GetUsersQuery(UserRole.Student, null, null), TestContext.Current.CancellationToken);

        var item = result.Items.Should().ContainSingle().Subject;
        (item.Id, item.MaskedPhone, item.MaskedEmail, item.Tier, item.HasAskTeacher, item.CanSuspend).Should().Be((student.Id, "010*****678", (string?)null, (PlanTier?)PlanTier.Base, false, true));
        item.SubjectIds.Should().BeEmpty();
        (result.PageNumber, result.TotalItems).Should().Be((1L, 1L));
    }

    [Fact]
    public async Task Handle_Teachers_ReturnsAssignedSubjectIdsAndInvitationPending()
    {
        var teacher = User.CreateTeacher("Teacher", "teacher@elmanhg.test");
        var subject = Subject.Create("Physics", 1, Guid.NewGuid());
        List<TeacherSubject> assignments = [TeacherSubject.Create(teacher, subject, _admin.Id), TeacherSubject.Create(User.CreateTeacher("Other", "other@elmanhg.test"), Subject.Create("Chemistry", 2, Guid.NewGuid()), _admin.Id)];
        _teacherSubjectRepository.FindAsync(Arg.Any<Expression<Func<TeacherSubject, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<TeacherSubject>, IQueryable<TeacherSubject>>?>(), Arg.Any<Func<IQueryable<TeacherSubject>, IOrderedQueryable<TeacherSubject>>?>(), Arg.Any<bool>())
            .Returns(call => assignments.Where(call.Arg<Expression<Func<TeacherSubject, bool>>>().Compile()).ToList());
        StubPage(teacher);

        var result = await _handler.Handle(new GetUsersQuery(UserRole.Teacher, null, null), TestContext.Current.CancellationToken);

        var item = result.Items.Should().ContainSingle().Subject;
        item.SubjectIds.Should().Equal(subject.Id);
        (item.InvitationPending, item.Tier, item.MaskedEmail).Should().Be((true, (PlanTier?)null, "t***@elmanhg.test"));
    }

    [Fact]
    public async Task Handle_AdminsWithOneActiveAdmin_MarksItNotSuspendable()
    {
        var other = User.CreateAdmin("Other", "other@elmanhg.test");
        StubPage(other);
        StubActiveAdminCount(1);

        var result = await _handler.Handle(new GetUsersQuery(UserRole.Admin, null, null), TestContext.Current.CancellationToken);

        result.Items.Should().ContainSingle().Which.CanSuspend.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_AdminsWithTwoActive_CurrentAdminNotSuspendableOtherIs()
    {
        var other = User.CreateAdmin("Other", "other@elmanhg.test");
        StubPage(_admin, other);
        StubActiveAdminCount(2);

        var result = await _handler.Handle(new GetUsersQuery(UserRole.Admin, null, null), TestContext.Current.CancellationToken);

        result.Items.Select(x => (x.Id, x.CanSuspend)).Should().Equal((_admin.Id, false), (other.Id, true));
    }

    private void StubPage(params User[] users)
    {
        _userRepository.FindPaginatedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<User, bool>>?>(), Arg.Any<Func<IQueryable<User>, IQueryable<User>>?>(), Arg.Any<Func<IQueryable<User>, IOrderedQueryable<User>>?>(), Arg.Any<bool>())
            .Returns(call => new PageData<User> { Items = [.. users], PageNumber = call.ArgAt<int>(0), PageSize = call.ArgAt<int>(1), TotalItems = users.Length, TotalPages = 1 });
    }

    private void StubActiveAdminCount(int count)
    {
        _userRepository.CountAsync(Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<User, bool>>?>()).Returns(count);
    }
}
