using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.TrainingData;
using Elmanhg.Infrastructure.Data.Context;

namespace Elmanhg.Infrastructure.TrainingData;

public class AttemptTrainingRecordRepository(AppDbContext context) : Repository<AttemptTrainingRecord>(context), IAttemptTrainingRecordRepository { }
