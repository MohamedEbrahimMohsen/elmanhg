using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Users.ReactivateUser;
using Elmanhg.Application.Users.Shared;
using Elmanhg.Domain.Identity;
using Elmanhg.Tests.Application.Features.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.Users.ReactivateUser;

public sealed class ReactivateUserHandlerTests : IDisposable
{
    private readonly UserManager<User> _userManager = UserManagerSubstitute.Create();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly ReactivateUserHandler _handler;

    public ReactivateUserHandlerTests()
    {
        _currentUserService.UserId.Returns(_actorId);
        _userManager.UpdateAsync(Arg.Any<User>()).Returns(IdentityResult.Success);
        _handler = new ReactivateUserHandler(_userManager, _currentUserService, _memoryCache);
    }

    [Fact]
    public async Task Handle_SuspendedUser_ReactivatesAndEvictsCache()
    {
        var teacher = Arrange(suspended: true);

        await _handler.Handle(new ReactivateUserCommand(teacher.Id), TestContext.Current.CancellationToken);

        (teacher.Status, teacher.UpdatedBy).Should().Be((UserStatus.Active, (Guid?)_actorId));
        await _userManager.Received(1).UpdateAsync(teacher);
        _memoryCache.TryGetValue(UserActiveCacheKey.For(teacher.Id), out _).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ActiveUser_ThrowsUserNotSuspended()
    {
        var teacher = Arrange(suspended: false);

        var act = () => _handler.Handle(new ReactivateUserCommand(teacher.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BusinessRuleViolationCoreException>()).Which.ErrorCode.Should().Be(DomainErrorCodes.UserNotSuspended);
        await _userManager.DidNotReceive().UpdateAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task Handle_UnknownUser_ThrowsUserNotFound()
    {
        var act = () => _handler.Handle(new ReactivateUserCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotFound);
        await _userManager.DidNotReceive().UpdateAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task Handle_UpdateFails_ThrowsUserModifiedConcurrently()
    {
        var teacher = Arrange(suspended: true);
        _userManager.UpdateAsync(teacher).Returns(IdentityResult.Failed());

        var act = () => _handler.Handle(new ReactivateUserCommand(teacher.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserModifiedConcurrently);
        _memoryCache.TryGetValue(UserActiveCacheKey.For(teacher.Id), out _).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new ReactivateUserCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _userManager.DidNotReceive().UpdateAsync(Arg.Any<User>());
    }

    public void Dispose() => _memoryCache.Dispose();

    private User Arrange(bool suspended)
    {
        var teacher = User.CreateTeacher("Teacher", "teacher@elmanhg.test");
        if (suspended)
        {
            teacher.Suspend();
        }

        _userManager.FindByIdAsync(teacher.Id.ToString()).Returns(teacher);
        _memoryCache.Set(UserActiveCacheKey.For(teacher.Id), false);
        return teacher;
    }
}
