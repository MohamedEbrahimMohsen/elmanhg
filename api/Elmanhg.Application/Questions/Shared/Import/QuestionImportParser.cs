using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.Spreadsheets;
using Elmanhg.Domain.Lessons;
using FluentValidation;

namespace Elmanhg.Application.Questions.Shared.Import;

public static class QuestionImportParser
{
    // Far wider than any type sheet's column set, so only stray cells far to the right are cut; it bounds the per-row work of a crafted sheet.
    private const int MaxSheetColumns = 256;

    public static async Task<QuestionImportParse> ParseAsync(byte[] content, Lesson lesson, ISpreadsheetReader reader, IValidator<QuestionFields> validator, ContentOptions options, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(content, writable: false);
        var workbook = reader.Read(stream, new SpreadsheetReadLimits(x => QuestionImportColumns.TypeForSheet(x) is not null, MaxSheetColumns, options.QuestionImportMaxRows));
        var sheets = workbook.Sheets
            .Select(x => (Sheet: x, Type: QuestionImportColumns.TypeForSheet(x.Name)))
            .Where(x => x.Type is not null)
            .ToList();
        var total = sheets.Sum(x => x.Sheet.Rows.Count);
        if (total == 0)
        {
            throw new BadRequestCoreException(ErrorCodes.QuestionImportEmpty);
        }

        if (total > options.QuestionImportMaxRows)
        {
            throw new BadRequestCoreException(ErrorCodes.QuestionImportTooManyRows, context: new Dictionary<string, object> { ["max"] = options.QuestionImportMaxRows });
        }

        List<QuestionImportRow> rows = [];
        List<QuestionImportRowError> errors = [];
        foreach (var (sheet, type) in sheets)
        {
            var columns = QuestionImportHeaders.Map(sheet, type!.Value, options, errors);
            foreach (var row in sheet.Rows)
            {
                var cells = new QuestionImportCells(sheet.Name, row, columns);
                var fields = QuestionImportRowMapper.Map(type.Value, cells, lesson, options);
                if (cells.Errors.Count > 0)
                {
                    errors.AddRange(cells.Errors);
                    continue;
                }

                var validation = await validator.ValidateAsync(fields, cancellationToken).ConfigureAwait(false);
                if (!validation.IsValid)
                {
                    errors.AddRange(validation.Errors
                        .Select(x => x.ErrorCode)
                        .Distinct()
                        .Select(code => new QuestionImportRowError(sheet.Name, row.Number, null, code)));
                    continue;
                }

                rows.Add(new QuestionImportRow(sheet.Name, row.Number, fields));
            }
        }

        return new QuestionImportParse(rows, errors, total);
    }
}
