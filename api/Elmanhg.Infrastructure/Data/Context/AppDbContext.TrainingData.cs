using Elmanhg.Domain.TrainingData;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.Data.Context;

public partial class AppDbContext
{
    public const string AttemptTrainingRecordsTable = "AttemptTrainingRecords";
    public const string AvatarTrainingRecordsTable = "AvatarTrainingRecords";
    public const string TeacherThreadTrainingRecordsTable = "TeacherThreadTrainingRecords";
    public const string EssayGradeTrainingRecordsTable = "EssayGradeTrainingRecords";
    public const string TeacherThreadTrainingTriggerIndex = "IX_TeacherThreadTrainingRecords_ThreadId_Trigger";

    public DbSet<AttemptTrainingRecord> AttemptTrainingRecords { get; set; }
    public DbSet<AvatarTrainingRecord> AvatarTrainingRecords { get; set; }
    public DbSet<TeacherThreadTrainingRecord> TeacherThreadTrainingRecords { get; set; }
    public DbSet<EssayGradeTrainingRecord> EssayGradeTrainingRecords { get; set; }

    private static void ConfigureTrainingData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AttemptTrainingRecord>(builder =>
        {
            builder.ToTable(AttemptTrainingRecordsTable);
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.StudentHash).IsRequired().HasMaxLength(Sha256HexLength);
            builder.Property(x => x.SessionKind).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.Property(x => x.GradedBy).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.Property(x => x.Answer).IsRequired().HasColumnType("jsonb");
            builder.Property(x => x.Grade).HasColumnType("jsonb");
            builder.Property(x => x.Score).HasPrecision(9, 2);
            builder.Property(x => x.NormalisedScore).HasPrecision(5, 4);
            builder.HasIndex(x => x.AttemptId).IsUnique();
            builder.HasIndex(x => x.OccurredAt);
            builder.HasIndex(x => x.StudentHash);
        });
        modelBuilder.Entity<AvatarTrainingRecord>(builder =>
        {
            builder.ToTable(AvatarTrainingRecordsTable);
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.StudentHash).IsRequired().HasMaxLength(Sha256HexLength);
            builder.Property(x => x.EntryPoint).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.Property(x => x.StudentText).IsRequired();
            builder.Property(x => x.AssistantText).IsRequired();
            builder.Property(x => x.Model).IsRequired().HasMaxLength(AiIdentifierMaxLength);
            builder.Property(x => x.PromptVersion).IsRequired().HasMaxLength(AiIdentifierMaxLength);
            builder.Property(x => x.Context).IsRequired().HasColumnType("jsonb");
            builder.HasIndex(x => x.StudentMessageId).IsUnique();
            builder.HasIndex(x => new { x.ConversationId, x.StudentMessagePosition });
            builder.HasIndex(x => x.OccurredAt);
            builder.HasIndex(x => x.StudentHash);
        });
        modelBuilder.Entity<TeacherThreadTrainingRecord>(builder =>
        {
            builder.ToTable(TeacherThreadTrainingRecordsTable);
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.StudentHash).IsRequired().HasMaxLength(Sha256HexLength);
            builder.Property(x => x.Trigger).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.Property(x => x.Context).IsRequired().HasColumnType("jsonb");
            builder.Property(x => x.Messages).IsRequired().HasColumnType("jsonb");
            builder.HasIndex(x => new { x.ThreadId, x.Trigger }).IsUnique().HasDatabaseName(TeacherThreadTrainingTriggerIndex);
            builder.HasIndex(x => x.OccurredAt);
            builder.HasIndex(x => x.StudentHash);
        });
        modelBuilder.Entity<EssayGradeTrainingRecord>(builder =>
        {
            builder.ToTable(EssayGradeTrainingRecordsTable);
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.StudentHash).IsRequired().HasMaxLength(Sha256HexLength);
            builder.Property(x => x.SessionKind).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.Property(x => x.Answer).IsRequired().HasColumnType("jsonb");
            builder.Property(x => x.Criteria).IsRequired().HasColumnType("jsonb");
            builder.Property(x => x.Justification).IsRequired();
            builder.Property(x => x.Score).HasPrecision(9, 2);
            builder.Property(x => x.NormalisedScore).HasPrecision(5, 4);
            builder.Property(x => x.Confidence).HasPrecision(5, 4);
            builder.Property(x => x.Model).IsRequired().HasMaxLength(AiIdentifierMaxLength);
            builder.Property(x => x.PromptVersion).IsRequired().HasMaxLength(AiIdentifierMaxLength);
            builder.HasIndex(x => x.EssayGradeId).IsUnique();
            builder.HasIndex(x => x.OccurredAt);
            builder.HasIndex(x => x.StudentHash);
        });
    }
}
