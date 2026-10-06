using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Avatar;
using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.MathStepGrading;
using Elmanhg.Domain.RuntimeSettings;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.SlaCalendars;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Domain.TrainingExports;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Core.Persistence;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Integration.Persistence;

public sealed class AppDbContextConflictMapTests(ApiFactory factory)
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(nameof(Session), ErrorCodes.SessionModifiedConcurrently)]
    [InlineData(nameof(QuestionMastery), ErrorCodes.SessionModifiedConcurrently)]
    [InlineData(nameof(Payment), ErrorCodes.PaymentModifiedConcurrently)]
    [InlineData(nameof(Subscription), ErrorCodes.SubscriptionModifiedConcurrently)]
    [InlineData(nameof(TeacherThread), ErrorCodes.TeacherThreadModifiedConcurrently)]
    [InlineData(nameof(AvatarConversation), ErrorCodes.AvatarConversationModifiedConcurrently)]
    [InlineData(nameof(TrainingExport), ErrorCodes.TrainingExportModifiedConcurrently)]
    [InlineData(nameof(EssayGrade), ErrorCodes.GradeModifiedConcurrently)]
    [InlineData(nameof(MathStepGrade), ErrorCodes.GradeModifiedConcurrently)]
    [InlineData(nameof(RuntimeSettingOverride), ErrorCodes.RuntimeSettingModifiedConcurrently)]
    [InlineData(nameof(ExamPeriod), ErrorCodes.ExamPeriodModifiedConcurrently)]
    public void TryTranslate_ConcurrencyOnVersionedEntity_ReturnsExistingCode(string entityName, string expectedCode)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var exception = ConcurrencyFailures.For(context, CreateVersionedEntity(entityName));

        var translated = AppDbContext.Conflicts.TryTranslate(exception, out var conflict);

        translated.Should().BeTrue();
        conflict!.ErrorCode.Should().Be(expectedCode);
        conflict.InnerException.Should().BeSameAs(exception);
        conflict.StatusCode.Should().Be(409);
    }

    [Theory]
    [InlineData(AppDbContext.RuntimeSettingKeyIndex, null, ErrorCodes.RuntimeSettingModifiedConcurrently)]
    [InlineData(AppDbContext.PaymobTransactionIndex, null, ErrorCodes.PaymentTransactionAlreadyRecorded)]
    [InlineData(AppDbContext.PaymentRefundTransactionIndex, null, ErrorCodes.PaymentTransactionAlreadyRecorded)]
    [InlineData("PK_QuestionImportBatches", "QuestionImportBatches", ErrorCodes.QuestionImportBatchConflict)]
    [InlineData(AppDbContext.InProgressSessionIndex, null, ErrorCodes.SessionAlreadyInProgress)]
    [InlineData(AppDbContext.AttemptPerQuestionIndex, null, DomainErrorCodes.SessionQuestionAlreadyAnswered)]
    [InlineData(AppDbContext.QuestionMasteryPerStudentIndex, null, ErrorCodes.SessionModifiedConcurrently)]
    [InlineData(AppDbContext.OneOpenExamIndex, null, ErrorCodes.ExamAlreadyInProgress)]
    [InlineData(AppDbContext.LessonOpeningPerStudentIndex, null, ErrorCodes.LessonAlreadyOpened)]
    [InlineData(AppDbContext.SubjectDefaultBlueprintIndex, null, ErrorCodes.ExamBlueprintModifiedConcurrently)]
    [InlineData(AppDbContext.UnitBlueprintIndex, null, ErrorCodes.ExamBlueprintModifiedConcurrently)]
    [InlineData(AppDbContext.AvatarMessagePositionIndex, null, ErrorCodes.AvatarConversationModifiedConcurrently)]
    [InlineData(AppDbContext.TeacherThreadTrainingTriggerIndex, null, ErrorCodes.TeacherThreadModifiedConcurrently)]
    [InlineData(AppDbContext.EssayGradeTrainingTriggerIndex, null, ErrorCodes.GradeModifiedConcurrently)]
    public void TryTranslate_UniqueViolation_ReturnsExistingCode(string? constraintName, string? tableName, string expectedCode)
    {
        var exception = UniqueViolationOn(constraintName, tableName);

        var translated = AppDbContext.Conflicts.TryTranslate(exception, out var conflict);

        translated.Should().BeTrue();
        conflict!.ErrorCode.Should().Be(expectedCode);
        conflict.InnerException.Should().BeSameAs(exception);
        conflict.StatusCode.Should().Be(409);
    }

    [Theory]
    [InlineData(AppDbContext.UserActivityDayIndex, PostgresErrorCodes.UniqueViolation)]
    [InlineData(AppDbContext.InProgressSessionIndex, PostgresErrorCodes.ForeignKeyViolation)]
    public void TryTranslate_UnmappedOrNonUniqueFailure_ReturnsFalse(string constraintName, string sqlState)
    {
        var exception = UniqueViolationOn(constraintName, null, sqlState);

        var translated = AppDbContext.Conflicts.TryTranslate(exception, out var conflict);

        translated.Should().BeFalse();
        conflict.Should().BeNull();
    }

    private static object CreateVersionedEntity(string entityName) => entityName switch
    {
        nameof(Session) => new SessionBuilder().Build(),
        nameof(QuestionMastery) => QuestionMastery.Start(Guid.NewGuid(), Guid.NewGuid(), new MasteryAttempt(Guid.NewGuid(), 1m, Now)),
        nameof(Payment) => Payment.Create(Guid.NewGuid(), SubscriptionPlan.Base, BillingPeriod.Monthly, 1, new Money(19900, "EGP")),
        nameof(Subscription) => new SubscriptionBuilder().Build(),
        nameof(TeacherThread) => new TeacherThreadBuilder().Build(),
        nameof(AvatarConversation) => new AvatarConversationBuilder().Build(),
        nameof(TrainingExport) => new TrainingExportBuilder().Build(),
        nameof(EssayGrade) => new EssayGradeBuilder().Build(),
        nameof(MathStepGrade) => new MathStepGradeBuilder().Build(),
        nameof(RuntimeSettingOverride) => RuntimeSettingOverride.Create("askTeacher.replySlaHours", "30", Guid.NewGuid()),
        nameof(ExamPeriod) => ExamPeriod.Create("Finals", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30), Guid.NewGuid()),
        _ => throw new ArgumentOutOfRangeException(nameof(entityName)),
    };

    private static DbUpdateException UniqueViolationOn(string? constraintName, string? tableName, string sqlState = PostgresErrorCodes.UniqueViolation) =>
        new("duplicate", new PostgresException("duplicate key value violates unique constraint", "ERROR", "ERROR", sqlState, tableName: tableName, constraintName: constraintName));
}
