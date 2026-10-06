namespace Core.Spreadsheets;

public interface ISpreadsheetWriter
{
    byte[] Write(IReadOnlyList<SpreadsheetSheetDefinition> sheets);
}
