using Elmanhg.Infrastructure.Messaging;
using Elmanhg.Tests.Infrastructure.OtpDelivery;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Infrastructure.Messaging;

public sealed class OutOfAppReminderOptionsValidatorTests
{
    private readonly OutOfAppReminderOptionsValidator _validator = new(Options.Create(OtpDeliveryTestSettings.Fake()));

    [Fact]
    public void Validate_DefaultsWithFakeEmail_Succeeds()
    {
        var result = _validator.Validate(null, new OutOfAppReminderOptions());

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_ThreadLinkNotHttps_Fails()
    {
        var result = _validator.Validate(null, new OutOfAppReminderOptions { ThreadLinkBaseUrl = "http://site.test/teacher/thread" });

        result.Failures.Should().ContainSingle().Which.Should().StartWith("OutOfAppReminders:ThreadLinkBaseUrl");
    }

    [Fact]
    public void Validate_UnknownTimeZone_Fails()
    {
        var result = _validator.Validate(null, new OutOfAppReminderOptions { TimeZone = "Mars/Olympus" });

        result.Failures.Should().ContainSingle().Which.Should().StartWith("OutOfAppReminders:TimeZone");
    }
}
