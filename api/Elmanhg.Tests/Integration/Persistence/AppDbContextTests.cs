using Core.DDD.Models;
using Core.Notifications.Entities;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Integration.Persistence;

public sealed class AppDbContextTests(ApiFactory factory)
{
    [Fact]
    public async Task Migrate_FreshDatabase_LeavesNoPendingMigrations()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pending = await context.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken);
        var applied = await context.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken);

        pending.Should().BeEmpty();
        applied.Should().SatisfyRespectively(first => first.Should().EndWith("_InitialCreate"), second => second.Should().EndWith("_AddUserProfileFields"), third => third.Should().EndWith("_AddTeacherSubjectScoping"), fourth => fourth.Should().EndWith("_AddAuditLogDiffAndAppendOnly"), fifth => fifth.Should().EndWith("_AddSubjectOrderAndUnits"), sixth => sixth.Should().EndWith("_AddLessons"), seventh => seventh.Should().EndWith("_AddLessonPublishedAt"), eighth => eighth.Should().EndWith("_AddQuestions"), ninth => ninth.Should().EndWith("_AddQuestionRejectionReason"), tenth => tenth.Should().EndWith("_AddQuestionImportBatches"), eleventh => eleventh.Should().EndWith("_AddQuestionRetiredAt"), twelfth => twelfth.Should().EndWith("_AddQuestionValidationQueue"), thirteenth => thirteenth.Should().EndWith("_AddAnswerNormalizationRules"), fourteenth => fourteenth.Should().EndWith("_AddSessionsAndAttempts"), fifteenth => fifteenth.Should().EndWith("_AddSessionVersion"), sixteenth => sixteenth.Should().EndWith("_AddQuestionMastery"), seventeenth => seventeenth.Should().EndWith("_AddExamBlueprints"), eighteenth => eighteenth.Should().EndWith("_AddExamSittings"), nineteenth => nineteenth.Should().EndWith("_AddOtpRecipientType"), twentieth => twentieth.Should().EndWith("_AddSubscriptionsAndPayments"), twentyFirst => twentyFirst.Should().EndWith("_AddPaymentWebhookState"), twentySecond => twentySecond.Should().EndWith("_AddPaymentRefunds"), twentyThird => twentyThird.Should().EndWith("_AddLessonOpenings"), twentyFourth => twentyFourth.Should().EndWith("_AddOnboardingAndFunnelEvents"));
    }

    [Fact]
    public void Model_Current_MatchesLatestMigrationSnapshot()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var hasPendingModelChanges = context.Database.HasPendingModelChanges();

        hasPendingModelChanges.Should().BeFalse();
    }

    [Fact]
    public async Task Migrate_NotificationData_CreatesJsonbColumn()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var dataType = await context.Database
            .SqlQuery<string>($"SELECT data_type AS \"Value\" FROM information_schema.columns WHERE table_name = {"Notifications"} AND column_name = {"Data"}")
            .SingleAsync(TestContext.Current.CancellationToken);

        dataType.Should().Be("jsonb");
    }

    [Fact]
    public async Task SaveChangesAsync_CoreAuditEntity_PersistsUtcTimestamps()
    {
        var code = $"probe-{Guid.CreateVersion7():N}";
        var template = NotificationTemplate.Create(Guid.CreateVersion7(), code, new LocalizedText("عنوان", "Title"), new LocalizedText("محتوى", "Content"), null);
        using (var writeScope = factory.Services.CreateScope())
        {
            var writeContext = writeScope.ServiceProvider.GetRequiredService<AppDbContext>();
            writeContext.NotificationTemplates.Add(template);
            await writeContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var readScope = factory.Services.CreateScope();
        var persisted = await readScope.ServiceProvider.GetRequiredService<AppDbContext>().NotificationTemplates
            .AsNoTracking()
            .SingleAsync(x => x.Id == template.Id, TestContext.Current.CancellationToken);

        persisted.Code.Should().Be(code);
        persisted.Title.English.Should().Be("Title");
        persisted.CreationDate.Offset.Should().Be(TimeSpan.Zero);
    }
}
