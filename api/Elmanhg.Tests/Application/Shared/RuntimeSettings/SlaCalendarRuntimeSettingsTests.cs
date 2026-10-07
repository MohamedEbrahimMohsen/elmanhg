using Core.Errors;
using Core.Settings;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.RuntimeSettings;
using Elmanhg.Application.Shared.RuntimeSettings.Definitions;
using Elmanhg.Tests.Fixtures.RuntimeSettings;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Shared.RuntimeSettings;

public sealed class SlaCalendarRuntimeSettingsTests
{
    private static readonly List<string> AllDays = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];
    private readonly RuntimeSettingRegistry _registry = FakeRuntimeSettings.DefaultRegistry();

    [Fact]
    public void Definitions_DefaultOptions_ReadOptionValues()
    {
        var options = new SlaCalendarOptions();

        var definitions = new SlaCalendarRuntimeSettings(Microsoft.Extensions.Options.Options.Create(options)).Definitions.ToDictionary(x => x.Key);

        definitions.Values.Should().OnlyContain(x => x.Group == nameof(RuntimeSettingGroup.SlaCalendar));
        definitions["slaCalendar.skipWeekends"].DefaultValue.GetBoolean().Should().BeTrue();
        definitions["slaCalendar.weekendDays"].DefaultValue.EnumerateArray().Select(x => x.GetString()).Should().Equal("Friday", "Saturday");
        definitions["slaCalendar.weekendDays"].AllowedValues.Should().Equal(AllDays);
        definitions["slaCalendar.timeZone"].DefaultValue.GetString().Should().Be("Africa/Cairo");
        definitions["slaCalendar.timeZone"].AllowedValues.Should().Equal(options.AllowedTimeZoneIds());
    }

    [Fact]
    public void Constraint_SkipOnAllSevenDays_IsBroken()
    {
        var values = Values(skipWeekends: true, AllDays);

        var act = () => _registry.EnsureConstraintsHold(values);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.SlaCalendarWeekendDaysInvalid);
    }

    [Fact]
    public void Constraint_SkipOffAllSevenDays_Holds()
    {
        var act = () => _registry.EnsureConstraintsHold(Values(skipWeekends: false, AllDays));

        act.Should().NotThrow();
    }

    [Fact]
    public void Constraint_SkipOnSixDays_Holds()
    {
        var act = () => _registry.EnsureConstraintsHold(Values(skipWeekends: true, AllDays.Skip(1).ToList()));

        act.Should().NotThrow();
    }

    private RuntimeSettingValues Values(bool skipWeekends, List<string> weekendDays) => RuntimeSettingValues.Defaults(_registry)
        .With(SlaCalendarRuntimeSettings.SkipWeekends.Name, RuntimeSettingJson.ToElement(skipWeekends))
        .With(SlaCalendarRuntimeSettings.WeekendDays.Name, RuntimeSettingJson.ToElement(weekendDays));
}
