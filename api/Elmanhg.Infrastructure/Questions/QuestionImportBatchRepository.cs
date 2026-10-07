using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.Questions;
using Elmanhg.Infrastructure.Data.Context;

namespace Elmanhg.Infrastructure.Questions;

public class QuestionImportBatchRepository(AppDbContext context) : Repository<QuestionImportBatch>(context), IQuestionImportBatchRepository { }
