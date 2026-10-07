using Core.DDD.Identity;
using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.SlaCalendars;
using Elmanhg.Infrastructure.Data.Context;

namespace Elmanhg.Infrastructure.SlaCalendars;

public class ExamPeriodRepository(AppDbContext context, ICurrentUser currentUser, TimeProvider timeProvider) : Repository<ExamPeriod>(context, currentUser, timeProvider), IExamPeriodRepository { }
