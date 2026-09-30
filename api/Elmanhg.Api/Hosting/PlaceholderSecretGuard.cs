using Npgsql;

namespace Elmanhg.Api.Hosting;

public static class PlaceholderSecretGuard
{
    // Every secret in the committed deploy/*.env.example files starts with this marker.
    public const string PlaceholderPrefix = "change-me";

    private const string OtpSecretKey = "CoreOtp:Secret";
    private const string ConnectionStringKey = "ConnectionStrings:DbConnectionString";

    public static readonly IReadOnlyList<string> SecretKeys = ["CoreJwt:Key", "CoreOtp:Secret", "AdminSeed:Password", "TrainingData:StudentIdHashKey", "AiService:ServiceToken", "Payments:Paymob:SecretKey", "Payments:Paymob:HmacSecret", "OtpDelivery:WhatsApp:AccessToken", "OtpDelivery:Email:ApiKey", "OtpDelivery:Sms:AuthHeaderValue", "FileStorage:S3SecretAccessKey"];

    public static void EnsureReplaced(IConfiguration configuration, IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            return;
        }

        var offending = SecretKeys
            .Where(x => IsPlaceholder(configuration[x]))
            .ToList();
        if (HasPlaceholderPassword(configuration[ConnectionStringKey]))
        {
            offending.Add(ConnectionStringKey);
        }

        if (string.IsNullOrWhiteSpace(configuration[OtpSecretKey]) && !offending.Contains(OtpSecretKey))
        {
            offending.Add(OtpSecretKey);
        }

        if (offending.Count > 0)
        {
            throw new InvalidOperationException($"The {environment.EnvironmentName} host refuses to start: replace the example or missing secrets {string.Join(", ", offending)} (docs/security.md).");
        }
    }

    private static bool HasPlaceholderPassword(string? connectionString) => !string.IsNullOrWhiteSpace(connectionString) && IsPlaceholder(new NpgsqlConnectionStringBuilder(connectionString).Password);

    private static bool IsPlaceholder(string? value) => value?.Trim().StartsWith(PlaceholderPrefix, StringComparison.OrdinalIgnoreCase) == true;
}
