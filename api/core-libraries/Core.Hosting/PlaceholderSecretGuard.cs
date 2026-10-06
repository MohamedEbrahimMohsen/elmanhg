using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Core.Hosting;

public static class PlaceholderSecretGuard
{
    public static void EnsureReplaced(IConfiguration configuration, IHostEnvironment environment, PlaceholderSecretRules rules)
    {
        if (environment.IsDevelopment())
        {
            return;
        }

        var offending = OffendingKeys(configuration, rules);
        if (offending.Count > 0)
        {
            throw new InvalidOperationException($"The {environment.EnvironmentName} host refuses to start: replace the example or missing secrets {string.Join(", ", offending)} ({rules.Guidance}).");
        }
    }

    private static List<string> OffendingKeys(IConfiguration configuration, PlaceholderSecretRules rules)
    {
        var offending = rules.SecretKeys
            .Where(x => rules.IsPlaceholder(configuration[x]))
            .ToList();
        offending.AddRange(rules.ValueChecks
            .Where(x => x.IsPlaceholder(configuration[x.Key]))
            .Select(x => x.Key)
            .ToList());
        var missing = rules.RequiredKeys
            .Where(x => string.IsNullOrWhiteSpace(configuration[x]) && !offending.Contains(x))
            .ToList();
        offending.AddRange(missing);
        return offending;
    }
}
