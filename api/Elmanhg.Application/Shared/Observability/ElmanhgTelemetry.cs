using System.Diagnostics;

namespace Elmanhg.Application.Shared.Observability;

public static class ElmanhgTelemetry
{
    public const string SourceName = "Elmanhg";

    public static readonly ActivitySource ActivitySource = new(SourceName);
}
