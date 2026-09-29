using Elmanhg.Application.Auth.Shared;
using Elmanhg.Domain.Identity;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Auth.Shared;

public sealed class AuthResultGeneratorTests
{
    [Fact]
    public void Generate_NewStudent_ReturnsNeedsOnboardingTrue()
    {
        var result = AuthResultGenerator.Generate(User.CreateStudentWithPhone("Ahmed", "01012345678"), "access", "refresh");

        result.User.NeedsOnboarding.Should().BeTrue();
    }

    [Fact]
    public void Generate_OnboardedStudent_ReturnsNeedsOnboardingFalse()
    {
        var student = User.CreateStudentWithPhone("Ahmed", "01012345678");
        student.ChooseSubjectInterests([], new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero));

        var result = AuthResultGenerator.Generate(student, "access", "refresh");

        result.User.NeedsOnboarding.Should().BeFalse();
    }

    [Fact]
    public void Generate_Admin_ReturnsNeedsOnboardingFalse()
    {
        var result = AuthResultGenerator.Generate(User.CreateAdmin("Admin", "admin@elmanhg.test"), "access", "refresh");

        result.User.NeedsOnboarding.Should().BeFalse();
    }
}
