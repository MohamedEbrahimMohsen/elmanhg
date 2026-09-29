using Core.Auditing;
using Core.EntityFrameworkCore.Auditing;
using Core.EntityFrameworkCore.Context;
using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Analytics;
using Elmanhg.Domain.Avatar;
using Elmanhg.Domain.ContentRetrieval;
using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.ReviewSessions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Domain.Teachers;
using Elmanhg.Domain.Units;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Infrastructure.Data.Context;

public class AppDbContext(DbContextOptions options, IMediator mediator, IAuditChangeCollector auditChangeCollector) : CoreDbContext<User, Role, Guid>(options, mediator, auditChangeCollector)
{
    // Enum names are short identifiers; the column width is a schema invariant, not a tunable.
    private const int EnumColumnMaxLength = 50;
    // A SHA-256 digest is 32 bytes, 64 lower-case hex characters; a schema invariant.
    private const int Sha256HexLength = 64;
    // ISO 4217 alphabetic codes are exactly three letters; a schema invariant.
    private const int CurrencyCodeLength = 3;
    // Paymob ids and references are short numeric strings; a schema invariant.
    private const int PaymobReferenceMaxLength = 100;

    public const string InProgressSessionIndex = "IX_Sessions_InProgressScope";
    public const string AttemptPerQuestionIndex = "IX_Attempts_SessionId_QuestionId";
    public const string QuestionMasteryPerStudentIndex = "IX_QuestionMasteries_StudentId_QuestionId";
    public const string LessonOpeningPerStudentIndex = "IX_LessonOpenings_StudentId_LessonId";
    public const string SubjectDefaultBlueprintIndex = "IX_ExamBlueprints_SubjectDefault";
    public const string UnitBlueprintIndex = "IX_ExamBlueprints_UnitId";
    public const string OneOpenExamIndex = "IX_Sessions_OneOpenExam";
    public const string OpenExamDeadlineIndex = "IX_Sessions_OpenExamDeadline";
    public const string PaymobTransactionIndex = "IX_Payments_PaymobTransactionId";
    public const string PaymentProviderOrderIndex = "IX_Payments_ProviderOrderId";
    public const string PaymentRefundTransactionIndex = "IX_Payments_RefundTransactionId";
    public const string PaymentOpenReviewIndex = "IX_Payments_OpenReview";
    public const string SubscriptionLapseIndex = "IX_Subscriptions_Status_CurrentPeriodEnd";

    public DbSet<Subject> Subjects { get; set; }
    public DbSet<TeacherSubject> TeacherSubjects { get; set; }
    public DbSet<CurriculumUnit> Units { get; set; }
    public DbSet<Lesson> Lessons { get; set; }
    public DbSet<LessonObjective> LessonObjectives { get; set; }
    public DbSet<LessonOpening> LessonOpenings { get; set; }
    public DbSet<Question> Questions { get; set; }
    public DbSet<QuestionRevision> QuestionRevisions { get; set; }
    public DbSet<QuestionImportBatch> QuestionImportBatches { get; set; }
    public DbSet<QuestionDecision> QuestionDecisions { get; set; }
    public DbSet<ReviewSession> ReviewSessions { get; set; }
    public DbSet<ReviewSessionOpening> ReviewSessionOpenings { get; set; }
    public DbSet<Session> Sessions { get; set; }
    public DbSet<SessionItem> SessionItems { get; set; }
    public DbSet<Attempt> Attempts { get; set; }
    public DbSet<QuestionMastery> QuestionMasteries { get; set; }
    public DbSet<ExamBlueprint> ExamBlueprints { get; set; }
    public DbSet<Subscription> Subscriptions { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<TeacherThread> TeacherThreads { get; set; }
    public DbSet<TeacherMessage> TeacherMessages { get; set; }
    public DbSet<FunnelEvent> FunnelEvents { get; set; }
    public DbSet<LessonContentChunk> LessonContentChunks { get; set; }
    public DbSet<LessonContentIndex> LessonContentIndexes { get; set; }
    public DbSet<AvatarMessageUsage> AvatarMessageUsages { get; set; }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException exception) when (exception.Entries.Any(x => x.Entity is Session or QuestionMastery))
        {
            throw new ConflictCoreException(ErrorCodes.SessionModifiedConcurrently, innerException: exception);
        }
        catch (DbUpdateConcurrencyException exception) when (exception.Entries.Any(x => x.Entity is Payment))
        {
            throw new ConflictCoreException(ErrorCodes.PaymentModifiedConcurrently, innerException: exception);
        }
        catch (DbUpdateConcurrencyException exception) when (exception.Entries.Any(x => x.Entity is Subscription))
        {
            throw new ConflictCoreException(ErrorCodes.SubscriptionModifiedConcurrently, innerException: exception);
        }
        catch (DbUpdateConcurrencyException exception) when (exception.Entries.Any(x => x.Entity is TeacherThread))
        {
            throw new ConflictCoreException(ErrorCodes.TeacherThreadModifiedConcurrently, innerException: exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: PaymobTransactionIndex or PaymentRefundTransactionIndex })
        {
            throw new ConflictCoreException(ErrorCodes.PaymentTransactionAlreadyRecorded, innerException: exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, TableName: nameof(QuestionImportBatches) })
        {
            // Two confirms of one import batch id passed the replay check together; the loser surfaces as the batch conflict, which the import pipeline resolves.
            throw new ConflictCoreException(ErrorCodes.QuestionImportBatchConflict, innerException: exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: InProgressSessionIndex })
        {
            throw new ConflictCoreException(ErrorCodes.SessionAlreadyInProgress, innerException: exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: AttemptPerQuestionIndex })
        {
            throw new ConflictCoreException(DomainErrorCodes.SessionQuestionAlreadyAnswered, innerException: exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: QuestionMasteryPerStudentIndex })
        {
            throw new ConflictCoreException(ErrorCodes.SessionModifiedConcurrently, innerException: exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: OneOpenExamIndex })
        {
            throw new ConflictCoreException(ErrorCodes.ExamAlreadyInProgress, innerException: exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: LessonOpeningPerStudentIndex })
        {
            throw new ConflictCoreException(ErrorCodes.LessonAlreadyOpened, innerException: exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: SubjectDefaultBlueprintIndex or UnitBlueprintIndex })
        {
            throw new ConflictCoreException(ErrorCodes.ExamBlueprintModifiedConcurrently, innerException: exception);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasPostgresExtension("vector");
        ConfigureUsers(modelBuilder);
        ConfigureSubjects(modelBuilder);
        ConfigureUnits(modelBuilder);
        ConfigureLessons(modelBuilder);
        ConfigureLessonOpenings(modelBuilder);
        ConfigureQuestions(modelBuilder);
        ConfigureQuestionImportBatches(modelBuilder);
        ConfigureReviewSessions(modelBuilder);
        ConfigureSessions(modelBuilder);
        ConfigureQuestionMastery(modelBuilder);
        ConfigureExamBlueprints(modelBuilder);
        ConfigureSubscriptions(modelBuilder);
        ConfigureTeacherThreads(modelBuilder);
        ConfigureTeacherSubjects(modelBuilder);
        ConfigureFunnelEvents(modelBuilder);
        ConfigureContentRetrieval(modelBuilder);
        ConfigureAvatar(modelBuilder);
        ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries(modelBuilder);
    }

    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(builder =>
        {
            builder.Property(x => x.DisplayName).IsRequired();
            builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.Property(x => x.SubjectInterestIds).IsRequired().HasDefaultValueSql("'{}'");
        });
    }

    private static void ConfigureSubjects(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Subject>(builder => builder.Property(x => x.Name).IsRequired());
    }

    private static void ConfigureUnits(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CurriculumUnit>(builder =>
        {
            builder.Property(x => x.Name).IsRequired();
            builder.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => new { x.SubjectId, x.Order });
        });
    }

    private static void ConfigureLessons(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Lesson>(builder =>
        {
            builder.Property(x => x.Name).IsRequired();
            builder.Property(x => x.State).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.Property(x => x.Explanation).IsRequired();
            builder.Property(x => x.Summary).IsRequired();
            builder.HasOne<CurriculumUnit>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
            builder.HasMany(x => x.Objectives).WithOne().HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => new { x.UnitId, x.Order });
        });
        modelBuilder.Entity<LessonObjective>(builder =>
        {
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.Text).IsRequired();
            builder.HasIndex(x => new { x.LessonId, x.Order });
        });
    }

    private static void ConfigureLessonOpenings(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LessonOpening>(builder =>
        {
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Lesson>().WithMany().HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => new { x.StudentId, x.LessonId }).IsUnique().HasDatabaseName(LessonOpeningPerStudentIndex);
        });
    }

    private static void ConfigureQuestions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Question>(builder =>
        {
            builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.Property(x => x.Difficulty).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.Property(x => x.ValidationStatus).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.Property(x => x.Stem).IsRequired();
            builder.Property(x => x.Body).IsRequired().HasColumnType("jsonb");
            builder.Property(x => x.GradingSpec).IsRequired().HasColumnType("jsonb");
            builder.Property(x => x.Explanation).IsRequired();
            builder.Property(x => x.Tags).IsRequired();
            builder.HasOne<Lesson>().WithMany().HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<LessonObjective>().WithMany().HasForeignKey(x => x.ObjectiveId).OnDelete(DeleteBehavior.Restrict);
            builder.HasMany(x => x.Revisions).WithOne().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
            builder.HasMany(x => x.Decisions).WithOne().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<QuestionImportBatch>().WithMany().HasForeignKey(x => x.ImportBatchId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => new { x.LessonId, x.ValidationStatus });
            builder.HasIndex(x => new { x.SubjectId, x.ValidationStatus });
        });
        modelBuilder.Entity<QuestionRevision>(builder =>
        {
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.Snapshot).IsRequired().HasColumnType("jsonb");
            builder.HasIndex(x => new { x.QuestionId, x.Version }).IsUnique();
        });
        modelBuilder.Entity<QuestionDecision>(builder =>
        {
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.Property(x => x.Difficulty).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.Property(x => x.DifficultyChangedFrom).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.HasIndex(x => new { x.QuestionId, x.DecidedAt });
        });
    }

    private static void ConfigureQuestionImportBatches(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QuestionImportBatch>(builder =>
        {
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.FileHash).IsRequired().HasMaxLength(Sha256HexLength);
            builder.HasOne<Lesson>().WithMany().HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureReviewSessions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReviewSession>(builder =>
        {
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
            builder.HasMany(x => x.Openings).WithOne().HasForeignKey(x => x.ReviewSessionId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => x.TeacherId);
        });
        modelBuilder.Entity<ReviewSessionOpening>(builder =>
        {
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.HasOne<Question>().WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => new { x.ReviewSessionId, x.QuestionId });
        });
    }

    private static void ConfigureSessions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Session>(builder =>
        {
            builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.Property(x => x.Scope).IsRequired().HasColumnType("jsonb");
            builder.Property(x => x.ScopeKey).IsRequired();
            builder.Property(x => x.ScorePercent).HasPrecision(5, 2);
            builder.Property(x => x.Version).IsRowVersion();
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            builder.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
            builder.HasMany(x => x.Attempts).WithOne().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => new { x.StudentId, x.StartedAt });
            builder.HasIndex(x => new { x.StudentId, x.Kind, x.ScopeKey }).IsUnique().HasFilter("\"SubmittedAt\" IS NULL AND \"IsDeleted\" = false").HasDatabaseName(InProgressSessionIndex);
            builder.HasIndex(x => x.StudentId, OneOpenExamIndex).IsUnique().HasFilter("\"Kind\" <> 'Quiz' AND \"SubmittedAt\" IS NULL AND \"IsDeleted\" = false");
            builder.HasIndex(x => x.Deadline, OpenExamDeadlineIndex).HasFilter("\"SubmittedAt\" IS NULL AND \"Deadline\" IS NOT NULL AND \"IsDeleted\" = false");
        });
        modelBuilder.Entity<SessionItem>(builder =>
        {
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.SavedAnswer).HasColumnType("jsonb");
            builder.HasOne<Question>().WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => new { x.SessionId, x.Position }).IsUnique();
            builder.HasIndex(x => new { x.SessionId, x.QuestionId }).IsUnique();
        });
        modelBuilder.Entity<Attempt>(builder =>
        {
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.Answer).IsRequired().HasColumnType("jsonb");
            builder.Property(x => x.Grade).HasColumnType("jsonb");
            builder.Property(x => x.GradedBy).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.Property(x => x.Score).HasPrecision(9, 2);
            builder.Property(x => x.NormalisedScore).HasPrecision(5, 4);
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Question>().WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => new { x.StudentId, x.QuestionId, x.CreatedAt });
            builder.HasIndex(x => new { x.SessionId, x.QuestionId }).IsUnique().HasDatabaseName(AttemptPerQuestionIndex);
        });
    }

    private static void ConfigureQuestionMastery(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QuestionMastery>(builder =>
        {
            builder.Property(x => x.LatestNormalisedScore).HasPrecision(5, 4);
            builder.Property(x => x.PreviousNormalisedScore).HasPrecision(5, 4);
            builder.Property(x => x.Version).IsRowVersion();
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Question>().WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => new { x.StudentId, x.QuestionId }).IsUnique().HasDatabaseName(QuestionMasteryPerStudentIndex);
        });
    }

    private static void ConfigureExamBlueprints(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExamBlueprint>(builder =>
        {
            builder.Property(x => x.TypeCounts).IsRequired().HasColumnType("jsonb");
            builder.Property(x => x.DifficultyMix).HasColumnType("jsonb");
            builder.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<CurriculumUnit>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => x.SubjectId).IsUnique().HasFilter("\"UnitId\" IS NULL AND \"IsDeleted\" = false").HasDatabaseName(SubjectDefaultBlueprintIndex);
            builder.HasIndex(x => x.UnitId).IsUnique().HasFilter("\"UnitId\" IS NOT NULL AND \"IsDeleted\" = false").HasDatabaseName(UnitBlueprintIndex);
        });
    }

    private static void ConfigureSubscriptions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Subscription>(builder =>
        {
            builder.Property(x => x.Plan).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.Property(x => x.Period).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.Property(x => x.PaymobReference).HasMaxLength(PaymobReferenceMaxLength);
            builder.Property(x => x.Version).IsRowVersion();
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => new { x.StudentId, x.Plan, x.CurrentPeriodEnd });
            builder.HasIndex(x => new { x.Status, x.CurrentPeriodEnd }, SubscriptionLapseIndex);
        });
        modelBuilder.Entity<Payment>(builder =>
        {
            builder.Property(x => x.Plan).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.Property(x => x.Period).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.Property(x => x.Currency).IsRequired().HasMaxLength(CurrencyCodeLength);
            builder.Property(x => x.PaymobTransactionId).HasMaxLength(PaymobReferenceMaxLength);
            builder.Property(x => x.ProviderOrderId).HasMaxLength(PaymobReferenceMaxLength);
            builder.Property(x => x.RefundTransactionId).HasMaxLength(PaymobReferenceMaxLength);
            builder.Property(x => x.ReviewReason).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.Property(x => x.RawWebhook).HasColumnType("jsonb").HasAnnotation(AuditChangeReader.ExcludedAnnotation, true);
            builder.Property(x => x.Version).IsRowVersion();
            builder.Ignore(x => x.Amount);
            builder.Ignore(x => x.NeedsReview);
            builder.Ignore(x => x.IsRefundable);
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Subscription>().WithMany().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => new { x.StudentId, x.CreationDate });
            builder.HasIndex(x => x.PaymobTransactionId, PaymobTransactionIndex).IsUnique().HasFilter("\"PaymobTransactionId\" IS NOT NULL");
            builder.HasIndex(x => x.ProviderOrderId, PaymentProviderOrderIndex).HasFilter("\"ProviderOrderId\" IS NOT NULL");
            builder.HasIndex(x => x.RefundTransactionId, PaymentRefundTransactionIndex).IsUnique().HasFilter("\"RefundTransactionId\" IS NOT NULL");
            builder.HasIndex(x => x.CreationDate);
            builder.HasIndex(x => x.CreationDate, PaymentOpenReviewIndex).HasFilter("\"ReviewReason\" IS NOT NULL AND \"ReviewResolvedAt\" IS NULL");
        });
    }

    private static void ConfigureTeacherThreads(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TeacherThread>(builder =>
        {
            builder.Property(x => x.Context).IsRequired().HasColumnType("jsonb");
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.Property(x => x.Version).IsRowVersion();
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            builder.HasMany(x => x.Messages).WithOne().HasForeignKey(x => x.ThreadId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => new { x.StudentId, x.SubmittedAt });
            builder.HasIndex(x => new { x.SubjectId, x.Status, x.SubmittedAt });
        });
        modelBuilder.Entity<TeacherMessage>(builder =>
        {
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.Property(x => x.Text).IsRequired();
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => new { x.ThreadId, x.CreatedAt });
            builder.HasIndex(x => x.ImageUrl).HasFilter("\"ImageUrl\" IS NOT NULL");
        });
    }

    private static void ConfigureTeacherSubjects(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TeacherSubject>(builder =>
        {
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => new { x.TeacherId, x.SubjectId }).IsUnique().HasFilter("\"IsDeleted\" = false");
        });
    }

    private static void ConfigureFunnelEvents(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FunnelEvent>(builder =>
        {
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => new { x.Type, x.OccurredAt });
            builder.HasIndex(x => x.AnonymousId);
        });
    }

    private static void ConfigureContentRetrieval(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LessonContentChunk>(builder =>
        {
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.Section).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.Property(x => x.SectionTitle).HasMaxLength(LessonContentChunk.SectionTitleMaxLength);
            builder.Property(x => x.Content).IsRequired();
            builder.Property(x => x.Embedding).HasColumnType($"vector({LessonContentChunk.EmbeddingDimensions})").IsRequired();
            builder.Property(x => x.EmbeddingModel).IsRequired().HasMaxLength(LessonContentChunk.EmbeddingModelMaxLength);
            builder.HasOne<Lesson>().WithMany().HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Question>().WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => x.LessonId);
            builder.HasIndex(x => x.QuestionId);
        });
        modelBuilder.Entity<LessonContentIndex>(builder =>
        {
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.EmbeddingModel).HasMaxLength(LessonContentChunk.EmbeddingModelMaxLength);
            builder.HasOne<Lesson>().WithMany().HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => x.LessonId).IsUnique();
        });
    }

    private static void ConfigureAvatar(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AvatarMessageUsage>(builder =>
        {
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.EntryPoint).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => new { x.StudentId, x.CreatedAt });
        });
    }

    private static void ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<Subject>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<TeacherSubject>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<CurriculumUnit>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<Lesson>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<LessonObjective>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<LessonOpening>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<Question>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<QuestionRevision>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<QuestionImportBatch>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<QuestionDecision>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<ReviewSession>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<ReviewSessionOpening>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<Session>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<SessionItem>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<Attempt>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<QuestionMastery>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<ExamBlueprint>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<Subscription>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<Payment>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<TeacherThread>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<TeacherMessage>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<FunnelEvent>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<LessonContentChunk>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<LessonContentIndex>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<AvatarMessageUsage>().HasQueryFilter(x => !x.IsDeleted);
    }
}
