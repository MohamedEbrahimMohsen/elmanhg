namespace Core.Observability;

public sealed record TelemetrySetup(string DefaultServiceName, IReadOnlyList<string> ActivitySources, IReadOnlyList<string> Meters, string HealthPath = TelemetrySetup.DefaultHealthPath)
{
    public const string DefaultHealthPath = "/health";
}
