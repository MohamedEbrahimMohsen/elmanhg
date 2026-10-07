using Core.DDD.Identity;
using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.Questions;
using Elmanhg.Infrastructure.Data.Context;

namespace Elmanhg.Infrastructure.Questions;

public class QuestionImportBatchRepository(AppDbContext context, ICurrentUser currentUser, TimeProvider timeProvider) : Repository<QuestionImportBatch>(context, currentUser, timeProvider), IQuestionImportBatchRepository { }
