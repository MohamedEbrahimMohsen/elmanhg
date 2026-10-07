namespace Core.Hosting;

public sealed class ReverseProxyOptions
{
    public const string SectionName = "ReverseProxy";

    // CIDR networks whose X-Forwarded-For / X-Forwarded-Proto are trusted. Loopback is always trusted.
    public List<string> TrustedNetworks { get; set; } = [];
}
