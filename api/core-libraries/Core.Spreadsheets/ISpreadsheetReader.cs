namespace Core.Spreadsheets;

public interface ISpreadsheetReader
{
    SpreadsheetWorkbook Read(Stream content, SpreadsheetReadLimits limits);
}
