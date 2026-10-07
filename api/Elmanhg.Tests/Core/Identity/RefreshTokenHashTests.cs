using Core.Identity.Tokens.RefreshToken;
using FluentAssertions;

namespace Elmanhg.Tests.Core.Identity;

public sealed class RefreshTokenHashTests
{
    [Fact]
    public void Compute_Token_ReturnsLowerHexSha256OfUtf8()
    {
        RefreshTokenHash.Compute("abc").Should().Be("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }

    [Fact]
    public void Compute_EmptyToken_ReturnsSha256OfEmptyInput()
    {
        RefreshTokenHash.Compute(string.Empty).Should().Be("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
    }
}
