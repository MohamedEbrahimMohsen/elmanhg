using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Application.Questions.Shared.Import;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.Spreadsheets;
using Elmanhg.Domain.Lessons;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Questions.PreviewQuestionImport;

public sealed class PreviewQuestionImportHandler(ILessonRepository lessonRepository, ISpreadsheetReader spreadsheetReader, IValidator<QuestionFields> questionValidator, IOptions<ContentOptions> contentOptions) : IRequestHandler<PreviewQuestionImportQuery, QuestionImportPreviewResult>
{
    public async Task<QuestionImportPreviewResult> Handle(PreviewQuestionImportQuery request, CancellationToken cancellationToken)
    {
        var lesson = await lessonRepository.GetWithObjectivesAsync(request.LessonId, asNoTracking: true, cancellationToken).ConfigureAwait(false);
        if (lesson is null)
        {
            throw new NotFoundCoreException(ErrorCodes.LessonNotFound);
        }

        var content = await QuestionImportFile.ReadAsync(request.File!, cancellationToken).ConfigureAwait(false);
        var parse = await QuestionImportParser.ParseAsync(content, lesson, spreadsheetReader, questionValidator, contentOptions.Value, cancellationToken).ConfigureAwait(false);
        var types = parse.Rows
            .GroupBy(x => x.Fields.Type.GetValueOrDefault())
            .OrderBy(x => x.Key)
            .Select(x => new QuestionImportTypeCount(x.Key, x.Count()))
            .ToList();
        return new QuestionImportPreviewResult(parse.TotalRows, parse.Rows.Count, types, parse.Errors.ToList());
    }
}
