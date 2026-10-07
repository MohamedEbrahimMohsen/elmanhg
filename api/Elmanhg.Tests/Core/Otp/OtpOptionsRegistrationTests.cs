using Core.OTP;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Core.Otp;

public sealed class OtpOptionsRegistrationTests
{
    [Fact]
    public void AddCoreOtp_NoPhoneSettings_FailsValidation()
    {
        using var provider = Provider([]);

        var act = () => provider.GetRequiredService<IOptions<OtpOptions>>().Value;

        act.Should().Throw<OptionsValidationException>().Which.Message.Should().Contain(nameof(OtpOptions.PhoneCodes)).And.Contain(nameof(OtpOptions.PhoneLength));
    }

    [Fact]
    public void AddCoreOtp_ConfiguredPhoneSettings_BindsExactlyConfiguredValues()
    {
        using var provider = Provider(new Dictionary<string, string?> { ["CoreOtp:PhoneCodes:0"] = "015", ["CoreOtp:PhoneLength"] = "11" });

        var options = provider.GetRequiredService<IOptions<OtpOptions>>().Value;

        (options.PhoneCodes, options.PhoneLength).Should().BeEquivalentTo((new List<string> { "015" }, 11));
    }

    private static ServiceProvider Provider(Dictionary<string, string?> values) => new ServiceCollection().AddCoreOtp(new ConfigurationBuilder().AddInMemoryCollection(values).Build()).BuildServiceProvider();
}
