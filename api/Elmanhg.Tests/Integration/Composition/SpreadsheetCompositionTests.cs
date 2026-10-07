using Core.Spreadsheets;
using Elmanhg.Application.Exceptions;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Integration.Composition;

public sealed class SpreadsheetCompositionTests(ApiFactory factory)
{
    [Fact]
    public void Resolve_SpreadsheetOptions_UseAppErrorCodeAndRightToLeft()
    {
        var options = factory.Services.GetRequiredService<IOptions<SpreadsheetOptions>>().Value;

        (options.UnreadableErrorCode, options.RightToLeft).Should().Be((ErrorCodes.SpreadsheetUnreadable, true));
    }

    [Fact]
    public async Task Start_ZeroUncompressedCap_FailsOptionsValidation()
    {
        await using var misconfigured = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.PostConfigure<SpreadsheetOptions>(options => options.MaxUncompressedSizeInMb = 0)));

        var act = () => misconfigured.CreateClient();

        act.Should().Throw<OptionsValidationException>().WithMessage("*MaxUncompressedSizeInMb must be greater than 0.*");
    }

    [Fact]
    public async Task Start_ZeroCompressedCap_FailsOptionsValidation()
    {
        await using var misconfigured = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.PostConfigure<SpreadsheetOptions>(options => options.MaxCompressedSizeInMb = 0)));

        var act = () => misconfigured.CreateClient();

        act.Should().Throw<OptionsValidationException>().WithMessage("*MaxCompressedSizeInMb must be greater than 0.*");
    }
}
