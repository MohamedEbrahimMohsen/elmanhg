using Core.Identity.Tokens.CurrentUser;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using System.Security.Claims;

namespace Elmanhg.Tests.Core.Identity;

public sealed class CurrentUserServiceTests
{
    private readonly IHttpContextAccessor _accessor = Substitute.For<IHttpContextAccessor>();

    [Fact]
    public void Role_RoleClaim_ReturnsRole()
    {
        _accessor.HttpContext.Returns(new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Teacher")], "Test")) });

        var role = new CurrentUserService(_accessor).Role;

        role.Should().Be("Teacher");
    }

    [Fact]
    public void UserId_NoHttpContext_ReturnsNull()
    {
        _accessor.HttpContext.Returns((HttpContext?)null);
        var currentUser = new CurrentUserService(_accessor);

        var identity = (currentUser.UserId, currentUser.UserName, currentUser.Role);

        identity.Should().Be(((Guid?)null, (string?)null, (string?)null));
    }
}
