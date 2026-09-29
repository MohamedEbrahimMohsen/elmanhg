using Elmanhg.Infrastructure.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace Elmanhg.Api.Hosting;

public static class ReverseProxyExtensions
{
    public static WebApplication UseReverseProxyForwardedHeaders(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<ReverseProxyOptions>>().Value;
        var forwardedHeaders = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto };
        foreach (var network in options.TrustedNetworks)
        {
            forwardedHeaders.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
        }

        app.UseForwardedHeaders(forwardedHeaders);
        return app;
    }
}
