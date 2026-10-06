namespace Core.Spreadsheets;

public sealed class SpreadsheetOptions
{
    public string UnreadableErrorCode { get; set; } = "SPREADSHEET_UNREADABLE";

    public bool RightToLeft { get; set; }
}
