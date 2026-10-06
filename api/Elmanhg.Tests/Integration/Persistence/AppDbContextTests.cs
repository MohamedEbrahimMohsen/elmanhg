using Core.DDD.Entities;
using Core.DDD.Models;
using Core.Notifications.Entities;
using Elmanhg.Domain.Avatar;
using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.MathStepGrading;
using Elmanhg.Domain.RuntimeSettings;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.SlaCalendars;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Domain.TrainingExports;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
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
        applied.Should().SatisfyRespectively(first => first.Should().EndWith("_InitialCreate"), second => second.Should().EndWith("_AddUserProfileFields"), third => third.Should().EndWith("_AddTeacherSubjectScoping"), fourth => fourth.Should().EndWith("_AddAuditLogDiffAndAppendOnly"), fifth => fifth.Should().EndWith("_AddSubjectOrderAndUnits"), sixth => sixth.Should().EndWith("_AddLessons"), seventh => seventh.Should().EndWith("_AddLessonPublishedAt"), eighth => eighth.Should().EndWith("_AddQuestions"), ninth => ninth.Should().EndWith("_AddQuestionRejectionReason"), tenth => tenth.Should().EndWith("_AddQuestionImportBatches"), eleventh => eleventh.Should().EndWith("_AddQuestionRetiredAt"), twelfth => twelfth.Should().EndWith("_AddQuestionValidationQueue"), thirteenth => thirteenth.Should().EndWith("_AddAnswerNormalizationRules"), fourteenth => fourteenth.Should().EndWith("_AddSessionsAndAttempts"), fifteenth => fifteenth.Should().EndWith("_AddSessionVersion"), sixteenth => sixteenth.Should().EndWith("_AddQuestionMastery"), seventeenth => seventeenth.Should().EndWith("_AddExamBlueprints"), eighteenth => eighteenth.Should().EndWith("_AddExamSittings"), nineteenth => nineteenth.Should().EndWith("_AddOtpRecipientType"), twentieth => twentieth.Should().EndWith("_AddSubscriptionsAndPayments"), twentyFirst => twentyFirst.Should().EndWith("_AddPaymentWebhookState"), twentySecond => twentySecond.Should().EndWith("_AddPaymentRefunds"), twentyThird => twentyThird.Should().EndWith("_AddLessonOpenings"), twentyFourth => twentyFourth.Should().EndWith("_AddOnboardingAndFunnelEvents"), twentyFifth => twentyFifth.Should().EndWith("_AddLessonContentIndex"), twentySixth => twentySixth.Should().EndWith("_AddTeacherThreads"), twentySeventh => twentySeventh.Should().EndWith("_AddAvatarMessageUsages"), twentyEighth => twentyEighth.Should().EndWith("_AddTeacherThreadClaims"), twentyNinth => twentyNinth.Should().EndWith("_AddTeacherVoiceReplies"), thirtieth => thirtieth.Should().EndWith("_AddAvatarConversations"), thirtyFirst => thirtyFirst.Should().EndWith("_AddAttemptStudentCreatedAtIndex"), thirtySecond => thirtySecond.Should().EndWith("_AddTeacherThreadSlaAndRatings"), thirtyThird => thirtyThird.Should().EndWith("_AddEssayGrades"), thirtyFourth => thirtyFourth.Should().EndWith("_AddTrainingRecords"), thirtyFifth => thirtyFifth.Should().EndWith("_AddDashboardMetrics"), thirtySixth => thirtySixth.Should().EndWith("_AddEssayGradeTimeTaken"), thirtySeventh => thirtySeventh.Should().EndWith("_AddTrainingExports"), thirtyEighth => thirtyEighth.Should().EndWith("_AddMathStepGrades"), thirtyNinth => thirtyNinth.Should().EndWith("_AddIssuedRefreshTokens"), fortieth => fortieth.Should().EndWith("_AddGradeReviews"), fortyFirst => fortyFirst.Should().EndWith("_AddRuntimeSettingOverrides"), fortySecond => fortySecond.Should().EndWith("_AddTeacherThreadOutOfAppReminders"), fortyThird => fortyThird.Should().EndWith("_AddSlaCalendar"), fortyFourth => fortyFourth.Should().EndWith("_AddAvatarConversationErasure"), fortyFifth => fortyFifth.Should().EndWith("_AddUserTermsAcceptance"), fortySixth => fortySixth.Should().EndWith("_AddOtpReissueWindow"));
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
    public void Model_VersionedEntities_MapVersionToXminRowVersion()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var versionProperties = context.Model.GetEntityTypes()
            .Select(x => x.FindDeclaredProperty(nameof(IVersioned.Version)))
            .OfType<IProperty>()
            .Where(x => x.IsConcurrencyToken)
            .ToList();
        var tokenTypes = versionProperties.Select(x => x.DeclaringType.ClrType).ToList();
        var versionedTypes = context.Model.GetEntityTypes()
            .Where(x => !x.IsOwned() && typeof(IVersioned).IsAssignableFrom(x.ClrType))
            .Select(x => x.ClrType)
            .ToList();

        tokenTypes.Should().BeEquivalentTo([typeof(Session), typeof(QuestionMastery), typeof(Payment), typeof(Subscription), typeof(TeacherThread), typeof(AvatarConversation), typeof(TrainingExport), typeof(EssayGrade), typeof(MathStepGrade), typeof(RuntimeSettingOverride), typeof(ExamPeriod)]);
        tokenTypes.Should().BeEquivalentTo(versionedTypes);
        versionProperties.Should().AllSatisfy(x => x.GetColumnName().Should().Be("xmin"));
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
    public async Task Migrate_LessonContentChunks_CreatesVectorColumn()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var udtName = await context.Database
            .SqlQuery<string>($"SELECT udt_name AS \"Value\" FROM information_schema.columns WHERE table_name = {"LessonContentChunks"} AND column_name = {"Embedding"}")
            .SingleAsync(TestContext.Current.CancellationToken);

        udtName.Should().Be("vector");
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
