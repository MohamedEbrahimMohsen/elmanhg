using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Infrastructure.Data.Context;

namespace Elmanhg.Infrastructure.TeacherThreads;

public class TeacherThreadSlaEventRepository(AppDbContext context) : Repository<TeacherThreadSlaEvent>(context), ITeacherThreadSlaEventRepository { }
