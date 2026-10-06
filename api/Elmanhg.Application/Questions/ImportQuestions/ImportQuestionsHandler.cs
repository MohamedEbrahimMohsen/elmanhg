using Core.DDD.Repositories;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Spreadsheets;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Application.Questions.Shared.Import;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.RichText;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Units;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Questions.ImportQuestions;

public sealed class ImportQuestionsHandler(ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository, IQuestionRepository questionRepository, IQuestionImportBatchRepository importBatchRepository, ISpreadsheetReader spreadsheetReader, IValidator<QuestionFields> questionValidator, IRichTextSanitizer richTextSanitizer, IOptions<ContentOptions> contentOptions, ICurrentUserService currentUserService) : IRequestHandler<ImportQuestionsCommand, ImportQuestionsResult>
{
    public async Task<ImportQuestionsResult> Handle(ImportQuestionsCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);

        var content = await QuestionImportFile.ReadAsync(request.File!, cancellationToken).ConfigureAwait(false);
        var hash = QuestionImportFile.Hash(content);
        var existing = await importBatchRepository.GetByIdAsync(request.BatchId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        if (existing is not null)
        {
            return QuestionImportReplay.Resolve(existing, request.LessonId, hash);
        }

        var lesson = await lessonRepository.GetWithObjectivesAsync(request.LessonId, asNoTracking: true, cancellationToken).ConfigureAwait(false);
        if (lesson is null)
        {
            throw new NotFoundCoreException(ErrorCodes.LessonNotFound);
        }

        var unit = await unitRepository.GetRequiredAsync(lesson.UnitId, ErrorCodes.UnitNotFound, cancellationToken, asNoTracking: true).ConfigureAwait(false);

        var parse = await QuestionImportParser.ParseAsync(content, lesson, spreadsheetReader, questionValidator, contentOptions.Value, cancellationToken).ConfigureAwait(false);
        if (parse.Errors.Count > 0)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.QuestionImportHasErrors, context: new Dictionary<string, object> { ["count"] = parse.Errors.Count });
        }

        var batch = QuestionImportBatch.Create(request.BatchId, lesson.Id, hash, parse.Rows.Count, userId);
        var questions = parse.Rows
            .Select(x => Question.CreateImported(lesson, unit, x.Fields.Type.GetValueOrDefault(), QuestionContentFactory.CreateContent(x.Fields, richTextSanitizer), QuestionContentFactory.CreateMetadata(x.Fields), batch.Id, userId))
            .ToList();

        await importBatchRepository.AddAsync(batch, cancellationToken).ConfigureAwait(false);
        await questionRepository.AddRangeAsync(questions, cancellationToken).ConfigureAwait(false);
        await questionRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new ImportQuestionsResult(batch.Id, questions.Count, Replayed: false);
    }
}
