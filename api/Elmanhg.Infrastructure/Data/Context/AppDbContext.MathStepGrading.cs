using Core.DDD.Models;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.MathStepGrading;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.Data.Context;

public partial class AppDbContext
{
    public const string MathStepGradePerQuestionIndex = "IX_MathStepGrades_SessionId_QuestionId";

    public DbSet<MathStepGrade> MathStepGrades { get; set; }

    private static void ConfigureMathStepGrades(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MathStepGrade>(builder =>
        {
            builder.Property(x => x.Answer).IsRequired().HasColumnType("jsonb");
            builder.Property(x => x.Feedback).HasColumnType("jsonb");
            builder.Property(x => x.Steps).HasColumnType("jsonb");
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.Property(x => x.ReviewReason).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.Property(x => x.FinalAnswerVerdict).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.Property(x => x.Score).HasPrecision(9, 2);
            builder.Property(x => x.NormalisedScore).HasPrecision(5, 4);
            builder.Property(x => x.Confidence).HasPrecision(5, 4);
            builder.Property(x => x.CostUsd).HasPrecision(12, 6);
            builder.Property(x => x.Model).HasMaxLength(AiIdentifierMaxLength);
            builder.Property(x => x.PromptVersion).HasMaxLength(AiIdentifierMaxLength);
            builder.OwnsOne(x => x.Retry, retry =>
            {
                retry.Property(x => x.Attempts).HasColumnName(nameof(RetrySchedule.Attempts));
                retry.Property(x => x.NextAttemptAt).HasColumnName(nameof(RetrySchedule.NextAttemptAt));
                retry.Property(x => x.LastErrorCode).HasColumnName(nameof(RetrySchedule.LastErrorCode)).HasMaxLength(AiIdentifierMaxLength);
                retry.HasIndex(x => x.NextAttemptAt).HasFilter("\"Status\" = 'Pending'").HasDatabaseName("IX_MathStepGrades_NextAttemptAt");
            });
            builder.Navigation(x => x.Retry).IsRequired();
            builder.Property(x => x.ReviewDecision).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.Property(x => x.ReviewedScore).HasPrecision(9, 2);
            builder.Property(x => x.ReviewedNormalisedScore).HasPrecision(5, 4);
            builder.Property(x => x.Version).IsRowVersion();
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Session>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Question>().WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => new { x.SessionId, x.QuestionId }).IsUnique().HasDatabaseName(MathStepGradePerQuestionIndex);
            builder.HasIndex(x => x.GradedAt).HasFilter("\"Status\" = 'Graded' AND \"AppliedAt\" IS NULL");
            builder.HasIndex(x => new { x.SubjectId, x.Status });
        });
    }
}
