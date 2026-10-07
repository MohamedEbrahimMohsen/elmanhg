using Core.DDD.Identity;
using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.Subjects;
using Elmanhg.Infrastructure.Data.Context;

namespace Elmanhg.Infrastructure.Subjects;

public class SubjectRepository(AppDbContext context, ICurrentUser currentUser, TimeProvider timeProvider) : Repository<Subject>(context, currentUser, timeProvider), ISubjectRepository { }
