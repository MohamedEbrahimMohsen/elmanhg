using Elmanhg.Application.Shared.Options;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Shared.Options;

public sealed class SlaCalendarOptionsValidatorTests
{
    private readonly SlaCalendarOptionsValidator _validator = new();

    [Fact]
    public void Validate_Defaults_Succeeds()
    {
        var result = _validator.Validate(null, new SlaCalendarOptions());

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_UnknownDayName_Fails()
    {
        var result = _validator.Validate(null, new SlaCalendarOptions { WeekendDays = "Fryday,Saturday" });

        result.Failures.Should().ContainSingle().Which.Should().Contain("WeekendDays").And.Contain("day names");
    }

    [Fact]
    public void Validate_RepeatedDay_Fails()
    {
        var result = _validator.Validate(null, new SlaCalendarOptions { WeekendDays = "Friday,Friday" });

        result.Failures.Should().ContainSingle().Which.Should().Contain("must not repeat");
    }

    [Fact]
    public void Validate_SevenDays_Fails()
    {
        var result = _validator.Validate(null, new SlaCalendarOptions { WeekendDays = "Sunday,Monday,Tuesday,Wednesday,Thursday,Friday,Saturday" });

        result.Failures.Should().ContainSingle().Which.Should().Contain("at least one day that counts");
    }

    [Fact]
    public void Validate_UnknownAllowedTimeZone_Fails()
    {
        var result = _validator.Validate(null, new SlaCalendarOptions { AllowedTimeZones = "Africa/Cairo,Mars/Base" });

        result.Failures.Should().ContainSingle().Which.Should().Contain("Mars/Base");
    }

    [Fact]
    public void Validate_TimeZoneNotAllowed_Fails()
    {
        var result = _validator.Validate(null, new SlaCalendarOptions { TimeZone = "UTC", AllowedTimeZones = "Africa/Cairo" });

        result.Failures.Should().ContainSingle().Which.Should().Contain("SlaCalendar:TimeZone");
    }
}
