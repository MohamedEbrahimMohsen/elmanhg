using Microsoft.Extensions.Options;

namespace Core.Spreadsheets;

public sealed class SpreadsheetOptionsValidator : IValidateOptions<SpreadsheetOptions>
{
    public ValidateOptionsResult Validate(string? name, SpreadsheetOptions options)
        => options.MaxUncompressedSizeInMb > 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"{nameof(SpreadsheetOptions.MaxUncompressedSizeInMb)} must be greater than 0.");
}
