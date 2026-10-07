using Microsoft.Extensions.Options;

namespace Core.Spreadsheets;

public sealed class SpreadsheetOptionsValidator : IValidateOptions<SpreadsheetOptions>
{
    public ValidateOptionsResult Validate(string? name, SpreadsheetOptions options)
    {
        List<string> failures = [];
        if (options.MaxUncompressedSizeInMb <= 0)
        {
            failures.Add($"{nameof(SpreadsheetOptions.MaxUncompressedSizeInMb)} must be greater than 0.");
        }

        if (options.MaxCompressedSizeInMb <= 0)
        {
            failures.Add($"{nameof(SpreadsheetOptions.MaxCompressedSizeInMb)} must be greater than 0.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
