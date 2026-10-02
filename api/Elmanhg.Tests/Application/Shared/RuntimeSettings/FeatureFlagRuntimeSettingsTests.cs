using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.RuntimeSettings;
using Elmanhg.Application.Shared.RuntimeSettings.Definitions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Shared.RuntimeSettings;

public sealed class FeatureFlagRuntimeSettingsTests
{
    [Fact]
    public void Definitions_RefundsEnabled_IsBooleanFeatureFlagDefaultingOff()
    {
        var definitions = new FeatureFlagRuntimeSettings(Microsoft.Extensions.Options.Options.Create(new ExamsOptions())).Definitions;

        var refunds = definitions.Single(x => x.Key == "features.refundsEnabled");

        (refunds.Group, refunds.Type, refunds.DefaultValue.GetBoolean()).Should().Be((RuntimeSettingGroup.Features, RuntimeSettingType.Boolean, false));
    }
}
