using Core.Auditing;
using Core.EntityFrameworkCore.Context;
using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.ReviewSessions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
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

    public const string InProgressSessionIndex = "IX_Sessions_InProgressScope";
    public const string AttemptPerQuestionIndex = "IX_Attempts_SessionId_QuestionId";

    public DbSet<Subject> Subjects { get; set; }
    public DbSet<TeacherSubject> TeacherSubjects { get; set; }
    public DbSet<CurriculumUnit> Units { get; set; }
    public DbSet<Lesson> Lessons { get; set; }
    public DbSet<LessonObjective> LessonObjectives { get; set; }
    public DbSet<Question> Questions { get; set; }
    public DbSet<QuestionRevision> QuestionRevisions { get; set; }
    public DbSet<QuestionImportBatch> QuestionImportBatches { get; set; }
    public DbSet<QuestionDecision> QuestionDecisions { get; set; }
    public DbSet<ReviewSession> ReviewSessions { get; set; }
    public DbSet<ReviewSessionOpening> ReviewSessionOpenings { get; set; }
    public DbSet<Session> Sessions { get; set; }
    public DbSet<SessionItem> SessionItems { get; set; }
    public DbSet<Attempt> Attempts { get; set; }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureUsers(modelBuilder);
        ConfigureSubjects(modelBuilder);
        ConfigureUnits(modelBuilder);
        ConfigureLessons(modelBuilder);
        ConfigureQuestions(modelBuilder);
        ConfigureQuestionImportBatches(modelBuilder);
        ConfigureReviewSessions(modelBuilder);
        ConfigureSessions(modelBuilder);
        ConfigureTeacherSubjects(modelBuilder);
        ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries(modelBuilder);
    }

    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(builder =>
        {
            builder.Property(x => x.DisplayName).IsRequired();
            builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
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
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            builder.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
            builder.HasMany(x => x.Attempts).WithOne().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => new { x.StudentId, x.StartedAt });
            builder.HasIndex(x => new { x.StudentId, x.Kind, x.ScopeKey }).IsUnique().HasFilter("\"SubmittedAt\" IS NULL AND \"IsDeleted\" = false").HasDatabaseName(InProgressSessionIndex);
        });
        modelBuilder.Entity<SessionItem>(builder =>
        {
            builder.Property(x => x.Id).ValueGeneratedNever();
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

    private static void ConfigureTeacherSubjects(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TeacherSubject>(builder =>
        {
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => new { x.TeacherId, x.SubjectId }).IsUnique().HasFilter("\"IsDeleted\" = false");
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
        modelBuilder.Entity<Question>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<QuestionRevision>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<QuestionImportBatch>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<QuestionDecision>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<ReviewSession>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<ReviewSessionOpening>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<Session>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<SessionItem>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<Attempt>().HasQueryFilter(x => !x.IsDeleted);
    }
}
