using Microsoft.Extensions.Options;

namespace Elmanhg.Infrastructure.AiService;

public sealed class AiServiceOptionsValidator : IValidateOptions<AiServiceOptions>
{
    // Shared-secret strength floor, the same as the AI service's own check.
    public const int MinServiceTokenLength = 32;

    public ValidateOptionsResult Validate(string? name, AiServiceOptions options)
    {
        List<string> failures = [];
        if (options.AttemptTimeoutSeconds > options.TotalTimeoutSeconds)
        {
            failures.Add("AiService:AttemptTimeoutSeconds must not exceed AiService:TotalTimeoutSeconds.");
        }

        if (options.Provider == AiServiceProvider.Http)
        {
            if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                failures.Add("AiService:BaseUrl must be an absolute http or https URL when the Http provider is selected.");
            }

            if (string.IsNullOrWhiteSpace(options.ServiceToken) || options.ServiceToken.Length < MinServiceTokenLength)
            {
                failures.Add("AiService:ServiceToken must be at least 32 characters when the Http provider is selected.");
            }
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
