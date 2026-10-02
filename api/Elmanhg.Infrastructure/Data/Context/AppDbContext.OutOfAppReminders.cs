using Elmanhg.Domain.TeacherThreads;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.Data.Context;

public partial class AppDbContext
{
    public DbSet<TeacherThreadOutOfAppReminder> TeacherThreadOutOfAppReminders { get; set; }

    private static void ConfigureTeacherThreadOutOfAppReminders(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TeacherThreadOutOfAppReminder>(builder =>
        {
            builder.Property(x => x.Stage).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.HasOne<TeacherThread>().WithMany().HasForeignKey(x => x.ThreadId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => x.ThreadId).IsUnique();
        });
    }
}
