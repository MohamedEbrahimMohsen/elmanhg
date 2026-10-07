using Core.DDD.Identity;
using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Infrastructure.Data.Context;

namespace Elmanhg.Infrastructure.ExamBlueprints;

public class ExamBlueprintRepository(AppDbContext context, ICurrentUser currentUser, TimeProvider timeProvider) : Repository<ExamBlueprint>(context, currentUser, timeProvider), IExamBlueprintRepository { }
