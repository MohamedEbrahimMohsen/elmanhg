using Core.EntityFrameworkCore.Conflicts;
using Elmanhg.Application.Exceptions;
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
using Microsoft.EntityFrameworkCore;
using Npgsql;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Infrastructure.Data.Context;

public partial class AppDbContext
{
    public static ConflictMap Conflicts { get; } = new ConflictMap(DetectUniqueViolation)
        .MapConcurrency<Session>(ErrorCodes.SessionModifiedConcurrently)
        .MapConcurrency<QuestionMastery>(ErrorCodes.SessionModifiedConcurrently)
        .MapConcurrency<Payment>(ErrorCodes.PaymentModifiedConcurrently)
        .MapConcurrency<Subscription>(ErrorCodes.SubscriptionModifiedConcurrently)
        .MapConcurrency<TeacherThread>(ErrorCodes.TeacherThreadModifiedConcurrently)
        .MapConcurrency<AvatarConversation>(ErrorCodes.AvatarConversationModifiedConcurrently)
        .MapConcurrency<TrainingExport>(ErrorCodes.TrainingExportModifiedConcurrently)
        .MapConcurrency<EssayGrade>(ErrorCodes.GradeModifiedConcurrently)
        .MapConcurrency<MathStepGrade>(ErrorCodes.GradeModifiedConcurrently)
        .MapConcurrency<RuntimeSettingOverride>(ErrorCodes.RuntimeSettingModifiedConcurrently)
        .MapConcurrency<ExamPeriod>(ErrorCodes.ExamPeriodModifiedConcurrently)
        .MapUniqueConstraint(RuntimeSettingKeyIndex, ErrorCodes.RuntimeSettingModifiedConcurrently)
        .MapUniqueConstraint(PaymobTransactionIndex, ErrorCodes.PaymentTransactionAlreadyRecorded)
        .MapUniqueConstraint(PaymentRefundTransactionIndex, ErrorCodes.PaymentTransactionAlreadyRecorded)
        // Two confirms of one import batch id passed the replay check together; the loser surfaces as the batch conflict, which the import pipeline resolves.
        .MapUniqueTable(nameof(QuestionImportBatches), ErrorCodes.QuestionImportBatchConflict)
        .MapUniqueConstraint(InProgressSessionIndex, ErrorCodes.SessionAlreadyInProgress)
        .MapUniqueConstraint(AttemptPerQuestionIndex, DomainErrorCodes.SessionQuestionAlreadyAnswered)
        .MapUniqueConstraint(QuestionMasteryPerStudentIndex, ErrorCodes.SessionModifiedConcurrently)
        .MapUniqueConstraint(OneOpenExamIndex, ErrorCodes.ExamAlreadyInProgress)
        .MapUniqueConstraint(LessonOpeningPerStudentIndex, ErrorCodes.LessonAlreadyOpened)
        .MapUniqueConstraint(SubjectDefaultBlueprintIndex, ErrorCodes.ExamBlueprintModifiedConcurrently)
        .MapUniqueConstraint(UnitBlueprintIndex, ErrorCodes.ExamBlueprintModifiedConcurrently)
        .MapUniqueConstraint(AvatarMessagePositionIndex, ErrorCodes.AvatarConversationModifiedConcurrently)
        // EF inserts the training row before the stale thread UPDATE, so a lost close/rate race surfaces here, not as a concurrency exception.
        .MapUniqueConstraint(TeacherThreadTrainingTriggerIndex, ErrorCodes.TeacherThreadModifiedConcurrently)
        .MapUniqueConstraint(EssayGradeTrainingTriggerIndex, ErrorCodes.GradeModifiedConcurrently);

    private static UniqueViolation? DetectUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgresException
            ? new UniqueViolation(postgresException.ConstraintName, postgresException.TableName)
            : null;
}
