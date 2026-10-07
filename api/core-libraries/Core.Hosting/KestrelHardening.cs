using Microsoft.AspNetCore.Hosting;

namespace Core.Hosting;

public static class KestrelHardening
{
    // Transport ceiling above typical upload caps; each endpoint's validator still enforces its own configurable cap.
    public const long DefaultMaxRequestBodyBytes = 10 * 1024 * 1024;

    public static IWebHostBuilder UseKestrelHardening(this IWebHostBuilder builder, long maxRequestBodyBytes = DefaultMaxRequestBodyBytes) => builder.ConfigureKestrel(options =>
    {
        options.AddServerHeader = false;
        options.Limits.MaxRequestBodySize = maxRequestBodyBytes;
    });
}
