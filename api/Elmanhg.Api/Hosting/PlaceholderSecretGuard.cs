using Elmanhg.Infrastructure.Hosting;
using Npgsql;

namespace Elmanhg.Api.Hosting;

public static class PlaceholderSecretGuard
{
    public const string PlaceholderPrefix = ConfigurationSecrets.PlaceholderPrefix;

    private const string OtpSecretKey = "CoreOtp:Secret";
    private const string ConnectionStringKey = "ConnectionStrings:DbConnectionString";

    public static readonly IReadOnlyList<string> SecretKeys = ConfigurationSecrets.Keys;

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

    private static bool IsPlaceholder(string? value) => ConfigurationSecrets.IsPlaceholder(value);
}
