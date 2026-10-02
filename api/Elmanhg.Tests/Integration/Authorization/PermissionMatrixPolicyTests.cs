using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace Elmanhg.Tests.Integration.Authorization;

public sealed class PermissionMatrixPolicyTests(ApiFactory factory)
{
    public static TheoryData<string, string, bool> Matrix => new()
    {
        { "Content.Browse", "Student", true }, { "Content.Browse", "Teacher", true }, { "Content.Browse", "Admin", true },
        { "Assessments.Take", "Student", true }, { "Assessments.Take", "Teacher", false }, { "Assessments.Take", "Admin", true },
        { "Content.Manage", "Student", false }, { "Content.Manage", "Teacher", false }, { "Content.Manage", "Admin", true },
        { "Lessons.Publish", "Student", false }, { "Lessons.Publish", "Teacher", false }, { "Lessons.Publish", "Admin", true },
        { "Questions.Validate", "Student", false }, { "Questions.Validate", "Teacher", true }, { "Questions.Validate", "Admin", false },
        { "Questions.ChangeDifficulty", "Student", false }, { "Questions.ChangeDifficulty", "Teacher", true }, { "Questions.ChangeDifficulty", "Admin", true },
        { "Blueprints.Manage", "Student", false }, { "Blueprints.Manage", "Teacher", false }, { "Blueprints.Manage", "Admin", true },
        { "AskTeacher.Submit", "Student", true }, { "AskTeacher.Submit", "Teacher", false }, { "AskTeacher.Submit", "Admin", false },
        { "AskTeacher.Reply", "Student", false }, { "AskTeacher.Reply", "Teacher", true }, { "AskTeacher.Reply", "Admin", true },
        { "AiGrades.Override", "Student", false }, { "AiGrades.Override", "Teacher", true }, { "AiGrades.Override", "Admin", true },
        { "Progress.ViewOwn", "Student", true }, { "Progress.ViewOwn", "Teacher", false }, { "Progress.ViewOwn", "Admin", false },
        { "Progress.ViewAny", "Student", false }, { "Progress.ViewAny", "Teacher", false }, { "Progress.ViewAny", "Admin", true },
        { "Subscription.Manage", "Student", true }, { "Subscription.Manage", "Teacher", false }, { "Subscription.Manage", "Admin", false },
        { "Avatar.Chat", "Student", true }, { "Avatar.Chat", "Teacher", false }, { "Avatar.Chat", "Admin", false },
        { "AvatarConversations.View", "Student", false }, { "AvatarConversations.View", "Teacher", false }, { "AvatarConversations.View", "Admin", true },
        { "Payments.Manage", "Student", false }, { "Payments.Manage", "Teacher", false }, { "Payments.Manage", "Admin", true },
        { "Dashboards.View", "Student", false }, { "Dashboards.View", "Teacher", false }, { "Dashboards.View", "Admin", true },
        { "TeacherStats.ViewOwn", "Student", false }, { "TeacherStats.ViewOwn", "Teacher", true }, { "TeacherStats.ViewOwn", "Admin", false },
        { "Users.Manage", "Student", false }, { "Users.Manage", "Teacher", false }, { "Users.Manage", "Admin", true },
        { "AuditLog.View", "Student", false }, { "AuditLog.View", "Teacher", false }, { "AuditLog.View", "Admin", true },
        { "TrainingData.Export", "Student", false }, { "TrainingData.Export", "Teacher", false }, { "TrainingData.Export", "Admin", true },
        { "Configuration.Manage", "Student", false }, { "Configuration.Manage", "Teacher", false }, { "Configuration.Manage", "Admin", true },
    };

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task AuthorizeAsync_RoleAgainstPolicy_MatchesPrdMatrix(string policy, string role, bool allowed)
    {
        using var scope = factory.Services.CreateScope();
        var authorizationService = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "Test"));

        var result = await authorizationService.AuthorizeAsync(principal, policy);

        result.Succeeded.Should().Be(allowed);
    }
}
