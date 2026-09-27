using Elmanhg.Application.Auth.Shared;
using Elmanhg.Domain.Identity;
using FluentAssertions;
using System.Security.Claims;

namespace Elmanhg.Tests.Application.Features.Auth.Shared;

public sealed class UserClaimsExtensionsTests
{
    [Fact]
    public void GetUserClaims_PhoneStudent_IncludesIdRoleAndPhone()
    {
        var user = User.CreateStudentWithPhone("Ahmed", "01012345678");

        var claims = user.GetUserClaims();

        claims.Should().ContainSingle(x => x.Type == ClaimTypes.NameIdentifier).Which.Value.Should().Be(user.Id.ToString("D"));
        claims.Should().ContainSingle(x => x.Type == ClaimTypes.Role).Which.Value.Should().Be("Student");
        claims.Should().ContainSingle(x => x.Type == ClaimTypes.MobilePhone).Which.Value.Should().Be("01012345678");
        claims.Should().NotContain(x => x.Type == ClaimTypes.Email);
    }

    [Fact]
    public void GetUserClaims_EmailAdmin_IncludesEmailAndOmitsPhone()
    {
        var user = User.CreateAdmin("Admin", "admin@elmanhg.test");

        var claims = user.GetUserClaims();

        claims.Should().ContainSingle(x => x.Type == ClaimTypes.Role).Which.Value.Should().Be("Admin");
        claims.Should().ContainSingle(x => x.Type == ClaimTypes.Email).Which.Value.Should().Be("admin@elmanhg.test");
        claims.Should().NotContain(x => x.Type == ClaimTypes.MobilePhone);
    }
}
