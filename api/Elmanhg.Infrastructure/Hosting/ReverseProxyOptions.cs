namespace Elmanhg.Infrastructure.Hosting;

public sealed class ReverseProxyOptions
{
    public const string SectionName = "ReverseProxy";

    // CIDR networks whose X-Forwarded-For / X-Forwarded-Proto are trusted (the compose network Caddy sits in). Loopback is always trusted.
    public List<string> TrustedNetworks { get; set; } = [];
}
