using System.ComponentModel.DataAnnotations;

namespace Elmanhg.Infrastructure.Hosting;

public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    [Required]
    public string ServiceName { get; set; } = "elmanhg-api";

    [Required]
    public string ServiceVersion { get; set; } = "dev";

    public string OtlpEndpoint { get; set; } = "";

    public string OtlpHeaders { get; set; } = "";

    [Range(0.0, 1.0)]
    public double TraceSampleRatio { get; set; } = 1.0;

    [Range(5, 3600)]
    public int MetricExportIntervalSeconds { get; set; } = 30;

    public Uri? ExportEndpoint => Uri.TryCreate(OtlpEndpoint, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) ? uri : null;
}
