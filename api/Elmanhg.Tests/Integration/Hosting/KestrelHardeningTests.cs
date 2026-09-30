using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Integration.Hosting;

public sealed class KestrelHardeningTests(ApiFactory factory)
{
    [Fact]
    public void Host_KestrelOptions_CapBodyAndHideServerHeader()
    {
        var options = factory.Services.GetRequiredService<IOptions<KestrelServerOptions>>().Value;

        (options.Limits.MaxRequestBodySize, options.AddServerHeader).Should().Be((10485760L, false));
    }
}
