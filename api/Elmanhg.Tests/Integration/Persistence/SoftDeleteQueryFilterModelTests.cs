using Core.DDD.Entities;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Integration.Persistence;

public sealed class SoftDeleteQueryFilterModelTests(ApiFactory factory)
{
    private static readonly string[] PreConventionFilteredTypes =
    [
        "Attempt", "AttemptTrainingRecord", "AuditLog", "AvatarConversation", "AvatarMessage", "AvatarMessageUsage", "AvatarTrainingRecord", "CurriculumUnit",
        "EssayGrade", "EssayGradeTrainingRecord", "ExamBlueprint", "ExamPeriod", "FunnelEvent", "IssuedRefreshToken", "Lesson", "LessonContentChunk",
        "LessonContentIndex", "LessonObjective", "LessonOpening", "MathStepGrade", "Notification", "NotificationTemplate", "Otp", "Payment",
        "Question", "QuestionDecision", "QuestionImportBatch", "QuestionMastery", "QuestionRevision", "ReviewSession", "ReviewSessionOpening", "RuntimeSettingOverride",
        "Session", "SessionItem", "Subject", "Subscription", "TeacherMessage", "TeacherSubject", "TeacherThread", "TeacherThreadOutOfAppReminder",
        "TeacherThreadSlaEvent", "TeacherThreadTrainingRecord", "TeacherVoiceDraft", "TrainingExport", "User", "UserActivityDay", "UserDevice",
    ];

    [Fact]
    public void Model_SoftDeletableRootEntityTypes_EachCarryOneQueryFilter()
    {
        using var scope = factory.Services.CreateScope();
        var model = scope.ServiceProvider.GetRequiredService<AppDbContext>().Model;

        var roots = model.GetEntityTypes().Where(x => typeof(ISoftDeletable).IsAssignableFrom(x.ClrType) && x.BaseType is null && !x.IsOwned()).ToList();

        roots.Should().NotBeEmpty().And.OnlyContain(x => x.GetDeclaredQueryFilters().Count == 1);
    }

    [Fact]
    public void Model_FilteredEntityTypes_MatchPreConventionSet()
    {
        using var scope = factory.Services.CreateScope();
        var model = scope.ServiceProvider.GetRequiredService<AppDbContext>().Model;

        var filtered = model.GetEntityTypes().Where(x => x.GetDeclaredQueryFilters().Count > 0).Select(x => x.ClrType.Name);

        filtered.Should().BeEquivalentTo(PreConventionFilteredTypes);
    }
}
