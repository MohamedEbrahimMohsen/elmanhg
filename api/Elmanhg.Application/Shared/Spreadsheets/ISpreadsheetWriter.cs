namespace Elmanhg.Application.Shared.Spreadsheets;

public interface ISpreadsheetWriter
{
    byte[] Write(IReadOnlyList<SpreadsheetSheetDefinition> sheets);
}
