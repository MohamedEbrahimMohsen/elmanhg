using Elmanhg.Domain.Identity;
using Elmanhg.Domain.SharedKernel;
using Microsoft.AspNetCore.Authorization;

namespace Elmanhg.Api.Authorization;

public static class PermissionMatrixPolicies
{
    private const string Student = nameof(UserRole.Student);
    private const string Teacher = nameof(UserRole.Teacher);
    private const string Admin = nameof(UserRole.Admin);

    public static AuthorizationBuilder AddPermissionMatrixPolicies(this AuthorizationBuilder builder)
    {
        return builder
            .AddPolicy(DefaultCodes.ContentBrowse, policy => policy.RequireRole(Student, Teacher, Admin))
            .AddPolicy(DefaultCodes.AssessmentsTake, policy => policy.RequireRole(Student, Admin))
            .AddPolicy(DefaultCodes.ContentManage, policy => policy.RequireRole(Admin))
            .AddPolicy(DefaultCodes.LessonsPublish, policy => policy.RequireRole(Admin))
            .AddPolicy(DefaultCodes.QuestionsValidate, policy => policy.RequireRole(Teacher))
            .AddPolicy(DefaultCodes.QuestionsChangeDifficulty, policy => policy.RequireRole(Teacher, Admin))
            .AddPolicy(DefaultCodes.BlueprintsManage, policy => policy.RequireRole(Admin))
            .AddPolicy(DefaultCodes.AskTeacherSubmit, policy => policy.RequireRole(Student))
            .AddPolicy(DefaultCodes.AskTeacherReply, policy => policy.RequireRole(Teacher, Admin))
            .AddPolicy(DefaultCodes.AiGradesOverride, policy => policy.RequireRole(Teacher, Admin))
            .AddPolicy(DefaultCodes.ProgressViewOwn, policy => policy.RequireRole(Student))
            .AddPolicy(DefaultCodes.ProgressViewAny, policy => policy.RequireRole(Admin))
            .AddPolicy(DefaultCodes.SubscriptionManage, policy => policy.RequireRole(Student))
            .AddPolicy(DefaultCodes.PaymentsManage, policy => policy.RequireRole(Admin))
            .AddPolicy(DefaultCodes.DashboardsView, policy => policy.RequireRole(Admin))
            .AddPolicy(DefaultCodes.TeacherStatsViewOwn, policy => policy.RequireRole(Teacher))
            .AddPolicy(DefaultCodes.UsersManage, policy => policy.RequireRole(Admin))
            .AddPolicy(DefaultCodes.AuditLogView, policy => policy.RequireRole(Admin))
            .AddPolicy(DefaultCodes.TrainingDataExport, policy => policy.RequireRole(Admin));
    }
}
