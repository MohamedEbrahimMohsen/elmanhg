namespace Core.Spreadsheets;

public sealed record SpreadsheetSheetDefinition(string Name, IReadOnlyList<SpreadsheetColumn> Columns, IReadOnlyList<IReadOnlyList<string>> Rows);

public sealed record SpreadsheetColumn(string Header, IReadOnlyList<string> AllowedValues, int Width);
