namespace Elmanhg.Api.Hosting;

public static class KestrelHardening
{
    // Transport ceiling above the largest business cap (5 MB uploads); each validator still enforces its own configurable cap.
    public const long MaxRequestBodyBytes = 10 * 1024 * 1024;

    public static IWebHostBuilder UseKestrelHardening(this IWebHostBuilder builder) => builder.ConfigureKestrel(options =>
    {
        options.AddServerHeader = false;
        options.Limits.MaxRequestBodySize = MaxRequestBodyBytes;
    });
}
