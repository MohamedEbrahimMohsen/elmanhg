using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Core.Identity;

public sealed class CurrentUserServiceExtensionsTests
{
    private const string ProbeCode = "PROBE_CODE";

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();

    [Fact]
    public void GetRequiredUserId_UserIdPresent_ReturnsUserId()
    {
        var userId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId);

        var result = _currentUserService.GetRequiredUserId(ProbeCode);

        result.Should().Be(userId);
    }

    [Fact]
    public void GetRequiredUserId_UserIdNull_ThrowsUnauthorizedWithGivenCode()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _currentUserService.GetRequiredUserId(ProbeCode);

        act.Should().Throw<UnauthorizedCoreException>().Which.ErrorCode.Should().Be(ProbeCode);
    }

    [Fact]
    public void GetRequiredUserId_UserIdEmpty_ThrowsUnauthorizedWithGivenCode()
    {
        _currentUserService.UserId.Returns(Guid.Empty);

        var act = () => _currentUserService.GetRequiredUserId(ProbeCode);

        act.Should().Throw<UnauthorizedCoreException>().Which.ErrorCode.Should().Be(ProbeCode);
    }
}
