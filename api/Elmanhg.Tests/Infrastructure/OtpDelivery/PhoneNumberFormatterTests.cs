using Elmanhg.Infrastructure.OtpDelivery;
using FluentAssertions;

namespace Elmanhg.Tests.Infrastructure.OtpDelivery;

public sealed class PhoneNumberFormatterTests
{
    [Fact]
    public void ToInternational_LocalNumber_ReplacesTrunkZeroWithCountryCode()
    {
        PhoneNumberFormatter.ToInternational("01012345678", "20").Should().Be("201012345678");
    }

    [Fact]
    public void ToInternational_AlreadyInternational_ReturnsUnchanged()
    {
        PhoneNumberFormatter.ToInternational("201012345678", "20").Should().Be("201012345678");
    }
}
