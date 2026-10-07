namespace Core.Spreadsheets;

public sealed record SpreadsheetWorkbook(IReadOnlyList<SpreadsheetSheet> Sheets);

public sealed record SpreadsheetSheet(string Name, IReadOnlyList<string> Headers, IReadOnlyList<SpreadsheetRow> Rows);

public sealed record SpreadsheetRow(int Number, IReadOnlyList<string> Cells);

public sealed record SpreadsheetReadLimits(Func<string, bool> IncludeSheet, int MaxColumns, int MaxDataRows);
