using Core.Spreadsheets;
using FluentAssertions;

namespace Elmanhg.Tests.Core.Spreadsheets;

public sealed class SpreadsheetOptionsValidatorTests
{
    private readonly SpreadsheetOptionsValidator _validator = new();

    [Fact]
    public void Validate_PositiveUncompressedCap_Succeeds()
    {
        var result = _validator.Validate(null, new SpreadsheetOptions { MaxUncompressedSizeInMb = 1 });

        result.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NonPositiveUncompressedCap_Fails(int maxUncompressedSizeInMb)
    {
        var result = _validator.Validate(null, new SpreadsheetOptions { MaxUncompressedSizeInMb = maxUncompressedSizeInMb });

        result.Failures.Should().ContainSingle().Which.Should().Be("MaxUncompressedSizeInMb must be greater than 0.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NonPositiveCompressedCap_Fails(int maxCompressedSizeInMb)
    {
        var result = _validator.Validate(null, new SpreadsheetOptions { MaxCompressedSizeInMb = maxCompressedSizeInMb });

        result.Failures.Should().ContainSingle().Which.Should().Be("MaxCompressedSizeInMb must be greater than 0.");
    }
}
