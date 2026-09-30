using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Users.Shared;
using Elmanhg.Application.Users.SuspendUser;
using Elmanhg.Domain.Identity;
using Elmanhg.Tests.Application.Features.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using System.Linq.Expressions;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.Users.SuspendUser;

public sealed class SuspendUserHandlerTests : IDisposable
{
    private readonly UserManager<User> _userManager = UserManagerSubstitute.Create();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly SuspendUserHandler _handler;

    public SuspendUserHandlerTests()
    {
        _currentUserService.UserId.Returns(_actorId);
        _userRepository.ExecuteInAdminRosterLockAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task>>()(call.Arg<CancellationToken>()));
        _userManager.UpdateSecurityStampAsync(Arg.Any<User>()).Returns(IdentityResult.Success);
        _handler = new SuspendUserHandler(_userManager, _userRepository, _currentUserService, _memoryCache);
    }

    [Fact]
    public async Task Handle_Student_SuspendsRotatesStampAndEvictsCache()
    {
        var student = Arrange(User.CreateStudentWithPhone("Mona", "01012345678"));

        await _handler.Handle(new SuspendUserCommand(student.Id), TestContext.Current.CancellationToken);

        student.Status.Should().Be(UserStatus.Suspended);
        student.UpdatedBy.Should().Be(_actorId);
        await _userManager.Received(1).UpdateSecurityStampAsync(student);
        _memoryCache.TryGetValue(UserActiveCacheKey.For(student.Id), out _).Should().BeFalse();
        await _userRepository.Received(1).ExecuteInAdminRosterLockAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>());
        await _userRepository.DidNotReceive().CountAsync(Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<User, bool>>?>());
    }

    [Fact]
    public async Task Handle_AdminWithTwoActiveAdmins_Suspends()
    {
        var admin = Arrange(User.CreateAdmin("Admin", "admin@elmanhg.test"));
        StubActiveAdminCount(2);

        await _handler.Handle(new SuspendUserCommand(admin.Id), TestContext.Current.CancellationToken);

        admin.Status.Should().Be(UserStatus.Suspended);
        await _userManager.Received(1).UpdateSecurityStampAsync(admin);
    }

    [Fact]
    public async Task Handle_LastActiveAdmin_ThrowsLastActiveAdmin()
    {
        var admin = Arrange(User.CreateAdmin("Admin", "admin@elmanhg.test"));
        StubActiveAdminCount(1);

        var act = () => _handler.Handle(new SuspendUserCommand(admin.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BusinessRuleViolationCoreException>()).Which.ErrorCode.Should().Be(DomainErrorCodes.LastActiveAdmin);
        await _userManager.DidNotReceive().UpdateSecurityStampAsync(Arg.Any<User>());
        _memoryCache.TryGetValue(UserActiveCacheKey.For(admin.Id), out _).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Self_ThrowsUserCannotSuspendSelf()
    {
        var self = User.CreateAdmin("Admin", "admin@elmanhg.test");
        _currentUserService.UserId.Returns(self.Id);
        Arrange(self);
        StubActiveAdminCount(3);

        var act = () => _handler.Handle(new SuspendUserCommand(self.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BusinessRuleViolationCoreException>()).Which.ErrorCode.Should().Be(DomainErrorCodes.UserCannotSuspendSelf);
        await _userManager.DidNotReceive().UpdateSecurityStampAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task Handle_UnknownUser_ThrowsUserNotFound()
    {
        var act = () => _handler.Handle(new SuspendUserCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotFound);
        await _userManager.DidNotReceive().UpdateSecurityStampAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task Handle_StampUpdateFails_ThrowsUserModifiedConcurrently()
    {
        var teacher = Arrange(User.CreateTeacher("Teacher", "teacher@elmanhg.test"));
        _userManager.UpdateSecurityStampAsync(teacher).Returns(IdentityResult.Failed());

        var act = () => _handler.Handle(new SuspendUserCommand(teacher.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserModifiedConcurrently);
        _memoryCache.TryGetValue(UserActiveCacheKey.For(teacher.Id), out _).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new SuspendUserCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _userRepository.DidNotReceive().ExecuteInAdminRosterLockAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>());
    }

    public void Dispose() => _memoryCache.Dispose();

    private User Arrange(User user)
    {
        _userManager.FindByIdAsync(user.Id.ToString()).Returns(user);
        _memoryCache.Set(UserActiveCacheKey.For(user.Id), true);
        return user;
    }

    private void StubActiveAdminCount(int count) => _userRepository.CountAsync(Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<User, bool>>?>()).Returns(count);
}
