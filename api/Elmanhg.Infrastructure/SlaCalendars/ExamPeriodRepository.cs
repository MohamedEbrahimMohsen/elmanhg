using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.SlaCalendars;
using Elmanhg.Infrastructure.Data.Context;

namespace Elmanhg.Infrastructure.SlaCalendars;

public class ExamPeriodRepository(AppDbContext context) : Repository<ExamPeriod>(context), IExamPeriodRepository { }
