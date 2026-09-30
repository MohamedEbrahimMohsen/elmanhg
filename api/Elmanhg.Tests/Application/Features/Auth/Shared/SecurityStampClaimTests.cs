using Elmanhg.Application.Auth.Shared;
using Elmanhg.Domain.Identity;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Auth.Shared;

public sealed class SecurityStampClaimTests
{
    [Fact]
    public void GetUserClaims_Always_CarriesStampFingerprintNotTheStamp()
    {
        var user = User.CreateStudentWithPhone("Ahmed", "01012345678");
        user.SecurityStamp = "RAW-SECURITY-STAMP";

        var claim = user.GetUserClaims().Single(x => x.Type == SecurityStampClaim.ClaimType);

        claim.Value.Should().Be(SecurityStampClaim.Fingerprint("RAW-SECURITY-STAMP")).And.NotContain("RAW-SECURITY-STAMP").And.HaveLength(64);
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
}
