using Core.Spreadsheets;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Core.Spreadsheets;

public sealed class SpreadsheetRegistrationTests
{
    [Fact]
    public async Task AddCoreSpreadsheets_ZeroUncompressedCap_FailsHostStart()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Services.AddCoreSpreadsheets(options => options.MaxUncompressedSizeInMb = 0);
        using var host = builder.Build();

        var act = () => host.StartAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<OptionsValidationException>().WithMessage("*MaxUncompressedSizeInMb must be greater than 0.*");
    }

    [Fact]
    public async Task AddCoreSpreadsheets_DefaultCaps_HostStarts()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Services.AddCoreSpreadsheets(_ => { });
        using var host = builder.Build();

        await host.StartAsync(TestContext.Current.CancellationToken);

        host.Services.GetRequiredService<IOptions<SpreadsheetOptions>>().Value.MaxUncompressedSizeInMb.Should().Be(100);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }
}
