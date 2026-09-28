namespace Elmanhg.Application.Shared.Spreadsheets;

public interface ISpreadsheetReader
{
    SpreadsheetWorkbook Read(Stream content, SpreadsheetReadLimits limits);
}
