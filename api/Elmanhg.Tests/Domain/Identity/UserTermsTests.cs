using Core.Errors;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.SharedKernel.Exceptions;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Identity;

public sealed class UserTermsTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AcceptTerms_CurrentVersion_RecordsVersionAndAcceptedAt()
    {
        var user = User.CreateStudentWithEmail("Mona", "mona@elmanhg.test");

        user.AcceptTerms(TermsVersions.Current, At);

        user.TermsVersion.Should().Be(TermsVersions.Current);
        user.TermsAcceptedAt.Should().Be(At);
        user.UpdationDate.Should().Be(At);
    }

    [Fact]
    public void AcceptTerms_UnknownVersion_ThrowsTermsVersionUnknown()
    {
        var user = User.CreateStudentWithPhone("Ahmed", "01012345678");

        var act = () => user.AcceptTerms("2020-01-01", At);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.TermsVersionUnknown);
        user.TermsVersion.Should().BeNull();
        user.TermsAcceptedAt.Should().BeNull();
    }

    [Fact]
    public void CreateStudentWithEmail_Always_HasNoTermsAcceptance()
    {
        var user = User.CreateStudentWithEmail("Mona", "mona@elmanhg.test");

        user.TermsVersion.Should().BeNull();
        user.TermsAcceptedAt.Should().BeNull();
    }

    [Theory]
    [InlineData("2026-10-05", true)]
    [InlineData("2026-10-04", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("2026-10-05 ", false)]
    public void IsKnown_Values_MatchesOnlyKnownVersions(string? version, bool expected)
    {
        TermsVersions.IsKnown(version).Should().Be(expected);
    }
}
