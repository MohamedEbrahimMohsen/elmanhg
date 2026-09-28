using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.Shared.Import;
using Elmanhg.Domain.Questions;
using MediatR;

namespace Elmanhg.Application.Questions.ImportQuestions;

public sealed class ImportQuestionsReplayBehaviour(IQuestionImportBatchRepository importBatchRepository) : IPipelineBehavior<ImportQuestionsCommand, ImportQuestionsResult>
{
    public async Task<ImportQuestionsResult> Handle(ImportQuestionsCommand request, RequestHandlerDelegate<ImportQuestionsResult> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }
        catch (ConflictCoreException exception) when (exception.ErrorCode == ErrorCodes.QuestionImportBatchConflict)
        {
            // Infrastructure raises this conflict when a concurrent confirm of the same batch id committed first; the stored batch decides replay or conflict.
            var existing = await importBatchRepository.GetByIdAsync(request.BatchId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
            if (existing is null)
            {
                throw;
            }

            var content = await QuestionImportFile.ReadAsync(request.File!, cancellationToken).ConfigureAwait(false);
            return QuestionImportReplay.Resolve(existing, request.LessonId, QuestionImportFile.Hash(content));
        }
    }
}
