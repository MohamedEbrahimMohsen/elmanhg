using Core.Spreadsheets;
using Elmanhg.Application.Exceptions;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
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
}
