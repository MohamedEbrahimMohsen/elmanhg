using System.Diagnostics;

namespace Elmanhg.Application.Shared.Observability;

public static class ElmanhgTelemetry
{
    public const string SourceName = "Elmanhg";
    public const string MetricPrefix = "elmanhg";

    public static readonly ActivitySource ActivitySource = new(SourceName);
}
