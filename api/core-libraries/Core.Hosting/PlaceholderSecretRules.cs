namespace Core.Hosting;

public sealed record PlaceholderValueCheck(string Key, Func<string?, bool> IsPlaceholder);

public sealed record PlaceholderSecretRules(IReadOnlyList<string> SecretKeys, Func<string?, bool> IsPlaceholder, IReadOnlyList<PlaceholderValueCheck> ValueChecks, IReadOnlyList<string> RequiredKeys, string Guidance);
