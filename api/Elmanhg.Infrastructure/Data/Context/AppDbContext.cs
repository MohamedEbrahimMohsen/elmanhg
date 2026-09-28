using Core.Auditing;
using Core.EntityFrameworkCore.Context;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Teachers;
using Elmanhg.Domain.Units;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.Data.Context;

public class AppDbContext(DbContextOptions options, IMediator mediator, IAuditChangeCollector auditChangeCollector) : CoreDbContext<User, Role, Guid>(options, mediator, auditChangeCollector)
{
    // Enum names are short identifiers; the column width is a schema invariant, not a tunable.
    private const int EnumColumnMaxLength = 50;

    public DbSet<Subject> Subjects { get; set; }
    public DbSet<TeacherSubject> TeacherSubjects { get; set; }
    public DbSet<CurriculumUnit> Units { get; set; }
    public DbSet<Lesson> Lessons { get; set; }
    public DbSet<LessonObjective> LessonObjectives { get; set; }
    public DbSet<Question> Questions { get; set; }
    public DbSet<QuestionRevision> QuestionRevisions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureUsers(modelBuilder);
        ConfigureSubjects(modelBuilder);
        ConfigureUnits(modelBuilder);
        ConfigureLessons(modelBuilder);
        ConfigureQuestions(modelBuilder);
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
            builder.HasIndex(x => new { x.LessonId, x.ValidationStatus });
            builder.HasIndex(x => new { x.SubjectId, x.ValidationStatus });
        });
        modelBuilder.Entity<QuestionRevision>(builder =>
        {
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.Snapshot).IsRequired().HasColumnType("jsonb");
            builder.HasIndex(x => new { x.QuestionId, x.Version }).IsUnique();
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
    }
}
