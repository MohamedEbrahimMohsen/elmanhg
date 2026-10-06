using Core.Spreadsheets;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Questions;

namespace Elmanhg.Application.Questions.Shared.Import;

public static class QuestionImportHeaders
{
    // Row 1 holds the column keys; data rows start at Excel row 2.
    private const int HeaderRow = 1;

    public static Dictionary<string, int> Map(SpreadsheetSheet sheet, QuestionType type, ContentOptions options, List<QuestionImportRowError> errors)
    {
        var known = QuestionImportColumns.For(type, options).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < sheet.Headers.Count; index++)
        {
            var header = sheet.Headers[index].Trim();
            if (header.Length == 0)
            {
                continue;
            }

            if (!known.Contains(header))
            {
                errors.Add(new QuestionImportRowError(sheet.Name, HeaderRow, header, ErrorCodes.QuestionImportColumnUnknown));
            }
            else if (!result.TryAdd(header, index))
            {
                errors.Add(new QuestionImportRowError(sheet.Name, HeaderRow, header, ErrorCodes.QuestionImportColumnDuplicate));
            }
        }

        return result;
    }
}
