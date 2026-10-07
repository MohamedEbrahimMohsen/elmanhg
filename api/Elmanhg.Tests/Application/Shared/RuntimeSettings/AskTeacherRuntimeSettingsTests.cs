using Core.Settings;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.RuntimeSettings.Definitions;
using Elmanhg.Tests.Fixtures.RuntimeSettings;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Shared.RuntimeSettings;

public sealed class AskTeacherRuntimeSettingsTests
{
    private readonly RuntimeSettingConstraint _constraint = new AskTeacherRuntimeSettings(Microsoft.Extensions.Options.Options.Create(new AskTeacherOptions()), Microsoft.Extensions.Options.Options.Create(new SubscriptionsOptions())).Constraints.Single();

    [Fact]
    public async Task Constraint_FirstNotBeforeSecond_IsUnsatisfied()
    {
        var values = await Values(first: 20, second: 20, sla: 24);

        _constraint.IsSatisfiedBy(values).Should().BeFalse();
    }

    [Fact]
    public async Task Constraint_SecondNotBeforeSla_IsUnsatisfied()
    {
        var values = await Values(first: 12, second: 24, sla: 24);

        _constraint.IsSatisfiedBy(values).Should().BeFalse();
    }

    [Fact]
    public async Task Constraint_Ordered_IsSatisfied()
    {
        var values = await Values(first: 12, second: 20, sla: 24);

        _constraint.IsSatisfiedBy(values).Should().BeTrue();
    }

    private static Task<RuntimeSettingValues> Values(int first, int second, int sla) => new FakeRuntimeSettings()
        .Set(AskTeacherRuntimeSettings.FirstReminderAfterHours, first)
        .Set(AskTeacherRuntimeSettings.SecondReminderAfterHours, second)
        .Set(AskTeacherRuntimeSettings.ReplySlaHours, sla)
        .GetValuesAsync(TestContext.Current.CancellationToken);
}
