using Core.Auditing;
using Core.EntityFrameworkCore.Context;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Teachers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.Data.Context;

public class AppDbContext(DbContextOptions options, IMediator mediator, IAuditChangeCollector auditChangeCollector) : CoreDbContext<User, Role, Guid>(options, mediator, auditChangeCollector)
{
    // Enum names are short identifiers; the column width is a schema invariant, not a tunable.
    private const int EnumColumnMaxLength = 50;

    public DbSet<Subject> Subjects { get; set; }
    public DbSet<TeacherSubject> TeacherSubjects { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureUsers(modelBuilder);
        ConfigureSubjects(modelBuilder);
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
    }
}
