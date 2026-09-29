using Microsoft.Extensions.Options;

namespace Elmanhg.Infrastructure.Storage;

public sealed class FileStorageOptionsValidator : IValidateOptions<FileStorageOptions>
{
    public ValidateOptionsResult Validate(string? name, FileStorageOptions options)
    {
        if (options.Provider != FileStorageProvider.S3)
        {
            return ValidateOptionsResult.Success;
        }

        List<string> failures = [];
        AddIfBlank(failures, options.S3BucketName, nameof(FileStorageOptions.S3BucketName));
        AddIfBlank(failures, options.S3AccessKeyId, nameof(FileStorageOptions.S3AccessKeyId));
        AddIfBlank(failures, options.S3SecretAccessKey, nameof(FileStorageOptions.S3SecretAccessKey));
        AddIfBlank(failures, options.S3Region, nameof(FileStorageOptions.S3Region));
        if (!string.IsNullOrWhiteSpace(options.S3ServiceUrl) && (!Uri.TryCreate(options.S3ServiceUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
        {
            failures.Add("FileStorage:S3ServiceUrl must be an absolute https URL.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void AddIfBlank(List<string> failures, string value, string key)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            failures.Add($"FileStorage:{key} is required when the S3 provider is selected.");
        }
    }
}
