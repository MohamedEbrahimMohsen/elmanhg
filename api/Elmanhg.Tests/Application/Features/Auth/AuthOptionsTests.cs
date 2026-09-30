using Elmanhg.Application.Shared.Options;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Auth;

public sealed class AuthOptionsTests
{
    [Fact]
    public void New_RefreshTokenCookieSecure_DefaultsToTrue()
    {
        var options = new AuthOptions();

        options.RefreshTokenCookieSecure.Should().BeTrue();
    }
}
