using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.Subjects;
using Elmanhg.Infrastructure.Data.Context;

namespace Elmanhg.Infrastructure.Subjects;

public class SubjectRepository(AppDbContext context) : Repository<Subject>(context), ISubjectRepository { }
