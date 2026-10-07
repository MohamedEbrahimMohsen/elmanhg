namespace Core.Spreadsheets;

public sealed class SpreadsheetOptions
{
    public string UnreadableErrorCode { get; set; } = "SPREADSHEET_UNREADABLE";

    public bool RightToLeft { get; set; }

    public int MaxUncompressedSizeInMb { get; set; } = 100;

    public int MaxCompressedSizeInMb { get; set; } = 100;
}
