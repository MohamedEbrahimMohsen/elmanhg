using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.TrainingExports;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.Data.Context;

public partial class AppDbContext
{
    // Job error codes are short identifiers; a schema invariant.
    private const int JobErrorCodeMaxLength = 100;

    public DbSet<TrainingExport> TrainingExports { get; set; }

    private static void ConfigureTrainingExports(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TrainingExport>(builder =>
        {
            builder.Property(x => x.Source).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.Property(x => x.LastErrorCode).HasMaxLength(JobErrorCodeMaxLength);
            builder.Property(x => x.FileKey).HasMaxLength(MediaUrlMaxLength);
            builder.Property(x => x.Sha256).HasMaxLength(Sha256HexLength);
            builder.Ignore(x => x.DownloadFileName);
            builder.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => x.NextAttemptAt).HasFilter("\"Status\" = 'Pending'");
            builder.HasIndex(x => x.ExpiresAt).HasFilter("\"Status\" = 'Completed'");
            builder.HasIndex(x => x.RequestedAt);
        });
    }
}
