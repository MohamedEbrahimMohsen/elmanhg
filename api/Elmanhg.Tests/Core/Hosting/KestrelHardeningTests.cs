using Core.Hosting;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Core.Hosting;

public sealed class KestrelHardeningTests
{
    [Fact]
    public async Task UseKestrelHardening_CustomLimit_SetsBodyCapAndHidesServerHeader()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrelHardening(4096);
        await using var app = builder.Build();

        var options = app.Services.GetRequiredService<IOptions<KestrelServerOptions>>().Value;

        (options.Limits.MaxRequestBodySize, options.AddServerHeader).Should().Be(((long?)4096L, false));
    }

    [Fact]
    public async Task UseKestrelHardening_NoArgument_UsesTenMegabyteDefault()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrelHardening();
        await using var app = builder.Build();

        var options = app.Services.GetRequiredService<IOptions<KestrelServerOptions>>().Value;

        options.Limits.MaxRequestBodySize.Should().Be(KestrelHardening.DefaultMaxRequestBodyBytes).And.Be(10485760L);
    }
}
