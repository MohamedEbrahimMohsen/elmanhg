using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Infrastructure.Data.Context;

namespace Elmanhg.Infrastructure.ExamBlueprints;

public class ExamBlueprintRepository(AppDbContext context) : Repository<ExamBlueprint>(context), IExamBlueprintRepository { }
