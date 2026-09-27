using Core.Errors;
using Elmanhg.Application.Auth.SeedAdmin;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Auth.SeedAdmin;

public sealed class SeedAdminHandlerTests
{
    private const string Email = "admin@elmanhg.test";
    private const string Password = "Admin-Password1";

    private readonly UserManager<User> _userManager = UserManagerSubstitute.Create();

    [Fact]
    public async Task Handle_EmailNotConfigured_DoesNothing()
    {
        var handler = CreateHandler(string.Empty);

        await handler.Handle(new SeedAdminCommand(), TestContext.Current.CancellationToken);

        await _userManager.DidNotReceive().FindByEmailAsync(Arg.Any<string>());
        await _userManager.DidNotReceive().CreateAsync(Arg.Any<User>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_AdminAlreadyExists_DoesNotCreate()
    {
        _userManager.FindByEmailAsync(Email).Returns(User.CreateAdmin("Admin", Email));
        var handler = CreateHandler(Email);

        await handler.Handle(new SeedAdminCommand(), TestContext.Current.CancellationToken);

        await _userManager.DidNotReceive().CreateAsync(Arg.Any<User>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_NewAdmin_CreatesAdminWithConfiguredPassword()
    {
        _userManager.CreateAsync(Arg.Any<User>(), Arg.Any<string>()).Returns(IdentityResult.Success);
        var handler = CreateHandler(Email);

        await handler.Handle(new SeedAdminCommand(), TestContext.Current.CancellationToken);

        await _userManager.Received(1).CreateAsync(Arg.Is<User>(x => x.Role == UserRole.Admin && x.Email == Email), Password);
    }

    [Fact]
    public async Task Handle_IdentityRejectsAdmin_ThrowsAdminSeedFailed()
    {
        _userManager.CreateAsync(Arg.Any<User>(), Arg.Any<string>()).Returns(IdentityResult.Failed(new IdentityError { Code = "PasswordTooShort" }));
        var handler = CreateHandler(Email);

        var act = () => handler.Handle(new SeedAdminCommand(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<InternalServerErrorCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.AdminSeedFailed);
    }

    private SeedAdminHandler CreateHandler(string email)
    {
        return new SeedAdminHandler(_userManager, Options.Create(new AdminSeedOptions { Email = email, Password = Password, DisplayName = "Admin" }));
    }
}
