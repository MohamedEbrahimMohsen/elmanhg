using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Questions;

namespace Elmanhg.Application.Questions.ImportQuestions;

public static class QuestionImportReplay
{
    public static ImportQuestionsResult Resolve(QuestionImportBatch existing, Guid lessonId, string fileHash)
    {
        if (!existing.Matches(lessonId, fileHash))
        {
            throw new ConflictCoreException(ErrorCodes.QuestionImportBatchConflict);
        }

        return new ImportQuestionsResult(existing.Id, existing.QuestionCount, Replayed: true);
    }
}
