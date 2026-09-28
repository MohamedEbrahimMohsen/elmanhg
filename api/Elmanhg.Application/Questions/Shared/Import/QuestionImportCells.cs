using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Spreadsheets;
using System.Globalization;

namespace Elmanhg.Application.Questions.Shared.Import;

public sealed class QuestionImportCells(string sheet, SpreadsheetRow row, IReadOnlyDictionary<string, int> columns)
{
    private static readonly HashSet<string> TrueTokens = new(["true", "yes", "1"], StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> FalseTokens = new(["false", "no", "0"], StringComparer.OrdinalIgnoreCase);

    public List<QuestionImportRowError> Errors { get; } = [];

    public string? Text(string column)
    {
        if (!columns.TryGetValue(column, out var index) || index >= row.Cells.Count || string.IsNullOrWhiteSpace(row.Cells[index]))
        {
            return null;
        }

        return row.Cells[index].Trim();
    }

    public List<string> List(string column)
    {
        return Text(column)?
            .Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToList() ?? [];
    }

    public bool? Boolean(string column)
    {
        var text = Text(column);
        return text switch
        {
            null => null,
            _ when TrueTokens.Contains(text) => true,
            _ when FalseTokens.Contains(text) => false,
            _ => Invalid<bool>(column),
        };
    }

    public decimal? Decimal(string column)
    {
        var text = Text(column);
        return text switch
        {
            null => null,
            _ when decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) => value,
            _ => Invalid<decimal>(column),
        };
    }

    public int? Integer(string column)
    {
        var text = Text(column);
        return text switch
        {
            null => null,
            _ when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) => value,
            _ => Invalid<int>(column),
        };
    }

    public TEnum? Enum<TEnum>(string column) where TEnum : struct, System.Enum
    {
        var text = Text(column);
        var name = text is null ? null : System.Enum.GetNames<TEnum>().FirstOrDefault(x => string.Equals(x, text, StringComparison.OrdinalIgnoreCase));
        return (text, name) switch
        {
            (null, _) => null,
            (_, not null) => System.Enum.Parse<TEnum>(name),
            _ => Invalid<TEnum>(column),
        };
    }

    public void AddError(string column, string code) => Errors.Add(new QuestionImportRowError(sheet, row.Number, column, code));

    private T? Invalid<T>(string column) where T : struct
    {
        AddError(column, ErrorCodes.QuestionImportCellInvalid);
        return null;
    }
}
