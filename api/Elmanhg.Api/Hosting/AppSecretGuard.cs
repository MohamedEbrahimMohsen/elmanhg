using Core.Hosting;
using Elmanhg.Infrastructure.Hosting;
using Npgsql;

namespace Elmanhg.Api.Hosting;

public static class AppSecretGuard
{
    private const string OtpSecretKey = "CoreOtp:Secret";
    private const string ConnectionStringKey = "ConnectionStrings:DbConnectionString";
    private const string Guidance = "docs/security.md";

    public static readonly IReadOnlyList<string> SecretKeys = ConfigurationSecrets.Keys;

    public static readonly PlaceholderSecretRules Rules = new(SecretKeys, ConfigurationSecrets.IsPlaceholder, [new(ConnectionStringKey, HasPlaceholderPassword)], [OtpSecretKey], Guidance);

    public static void EnsureReplaced(IConfiguration configuration, IHostEnvironment environment) => PlaceholderSecretGuard.EnsureReplaced(configuration, environment, Rules);

    private static bool HasPlaceholderPassword(string? connectionString) => !string.IsNullOrWhiteSpace(connectionString) && ConfigurationSecrets.IsPlaceholder(new NpgsqlConnectionStringBuilder(connectionString).Password);
}
