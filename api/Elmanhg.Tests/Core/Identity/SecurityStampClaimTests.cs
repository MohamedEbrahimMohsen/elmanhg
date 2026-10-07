using Core.Identity.Tokens.AccessToken;
using FluentAssertions;

namespace Elmanhg.Tests.Core.Identity;

public sealed class SecurityStampClaimTests
{
    private const string EmptySha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    [Fact]
    public void ClaimType_Always_IsSecurityStampHash()
    {
        SecurityStampClaim.ClaimType.Should().Be("security_stamp_hash");
    }

    [Fact]
    public void Fingerprint_Stamp_ReturnsLowerHexSha256()
    {
        SecurityStampClaim.Fingerprint("stamp-one").Should().Be("798751b9a682bbdaa202876c45827cb550273da38a1f2f156ec8ae1044bf34c4");
    }

    [Fact]
    public void Fingerprint_NullStamp_HashesEmptyString()
    {
        SecurityStampClaim.Fingerprint(null).Should().Be(EmptySha256);
    }

    [Fact]
    public void Matches_SameStamp_ReturnsTrue()
    {
        var fingerprint = SecurityStampClaim.Fingerprint("stamp-one");

        SecurityStampClaim.Matches(fingerprint, "stamp-one").Should().BeTrue();
    }

    [Fact]
    public void Matches_RotatedStamp_ReturnsFalse()
    {
        var fingerprint = SecurityStampClaim.Fingerprint("stamp-one");

        SecurityStampClaim.Matches(fingerprint, "stamp-two").Should().BeFalse();
    }

    [Fact]
    public void Matches_TruncatedFingerprint_ReturnsFalse()
    {
        var fingerprint = SecurityStampClaim.Fingerprint("stamp-one")[..63];

        SecurityStampClaim.Matches(fingerprint, "stamp-one").Should().BeFalse();
    }

    [Fact]
    public void Matches_UpperCaseFingerprint_ReturnsFalse()
    {
        var fingerprint = SecurityStampClaim.Fingerprint("stamp-one").ToUpperInvariant();

        SecurityStampClaim.Matches(fingerprint, "stamp-one").Should().BeFalse();
    }
}
