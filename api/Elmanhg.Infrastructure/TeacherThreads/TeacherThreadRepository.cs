using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Infrastructure.Data.Context;

namespace Elmanhg.Infrastructure.TeacherThreads;

public class TeacherThreadRepository(AppDbContext context) : Repository<TeacherThread>(context), ITeacherThreadRepository { }
