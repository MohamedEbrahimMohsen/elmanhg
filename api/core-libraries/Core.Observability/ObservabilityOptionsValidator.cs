using Microsoft.Extensions.Options;

namespace Core.Observability;

public sealed class ObservabilityOptionsValidator : IValidateOptions<ObservabilityOptions>
{
    public ValidateOptionsResult Validate(string? name, ObservabilityOptions options)
    {
        List<string> failures = [];
        if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint) && options.ExportEndpoint is null)
        {
            failures.Add($"Observability:OtlpEndpoint '{options.OtlpEndpoint}' must be an absolute http or https URI such as http://otel-collector:4317.");
        }

        if (!string.IsNullOrWhiteSpace(options.OtlpHeaders) && !AreKeyValuePairs(options.OtlpHeaders))
        {
            failures.Add("Observability:OtlpHeaders must be comma-separated key=value pairs.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static bool AreKeyValuePairs(string headers) => headers
        .Split(',')
        .All(x => x.IndexOf('=') > 0 && !string.IsNullOrWhiteSpace(x[..x.IndexOf('=')]));
}
