using Elmanhg.Domain.SlaCalendars;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.Data.Context;

public partial class AppDbContext
{
    public DbSet<ExamPeriod> ExamPeriods { get; set; }

    private static void ConfigureSlaCalendars(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExamPeriod>(builder =>
        {
            builder.Property(x => x.Name).IsRequired();
            builder.HasIndex(x => new { x.StartDate, x.EndDate });
            builder.ToTable(x => x.HasCheckConstraint("CK_ExamPeriods_DateRange", "\"EndDate\" >= \"StartDate\""));
        });
    }
}
