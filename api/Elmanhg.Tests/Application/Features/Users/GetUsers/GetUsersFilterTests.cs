using Elmanhg.Application.Users.GetUsers;
using Elmanhg.Domain.Identity;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Users.GetUsers;

public sealed class GetUsersFilterTests
{
    private const string Phone = "01012345678";
    private const string Email = "Mona.Ali@Example.test";

    [Fact]
    public void Build_SearchFragment_MatchesDisplayNameIgnoringCase()
    {
        var student = User.CreateStudentWithPhone("Mona Ali", Phone);

        Matches(new GetUsersQuery(UserRole.Student, "  mONA ", null), student).Should().BeTrue();
    }

    [Fact]
    public void Build_ExactPhone_Matches()
    {
        var student = User.CreateStudentWithPhone("Mona", Phone);

        Matches(new GetUsersQuery(UserRole.Student, Phone, null), student).Should().BeTrue();
    }

    [Fact]
    public void Build_PartialPhone_DoesNotMatch()
    {
        var student = User.CreateStudentWithPhone("Mona", Phone);

        Matches(new GetUsersQuery(UserRole.Student, "0101234", null), student).Should().BeFalse();
    }

    [Fact]
    public void Build_EmailInOtherCase_Matches()
    {
        var student = User.CreateStudentWithEmail("Mona", Email);
        student.NormalizedEmail = Email.ToUpperInvariant();

        Matches(new GetUsersQuery(UserRole.Student, "mona.ali@example.TEST", null), student).Should().BeTrue();
    }

    [Fact]
    public void Build_Role_ExcludesOtherRoles()
    {
        var teacher = User.CreateTeacher("Mona", Email);

        Matches(new GetUsersQuery(UserRole.Student, null, null), teacher).Should().BeFalse();
    }

    [Fact]
    public void Build_Status_ExcludesOtherStatus()
    {
        var student = User.CreateStudentWithPhone("Mona", Phone);
        student.Suspend();

        Matches(new GetUsersQuery(UserRole.Student, null, UserStatus.Active), student).Should().BeFalse();
    }

    private static bool Matches(GetUsersQuery query, User user) => GetUsersFilter.Build(query).Compile()(user);
}
