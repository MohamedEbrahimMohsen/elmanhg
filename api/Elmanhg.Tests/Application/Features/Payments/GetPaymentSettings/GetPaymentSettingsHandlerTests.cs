using Elmanhg.Application.Payments.GetPaymentSettings;
using Elmanhg.Application.Shared.RuntimeSettings.Definitions;
using Elmanhg.Tests.Fixtures.RuntimeSettings;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Payments.GetPaymentSettings;

public sealed class GetPaymentSettingsHandlerTests
{
    [Fact]
    public async Task Handle_FlagNeverSet_ReturnsRefundsDisabled()
    {
        var result = await new GetPaymentSettingsHandler(new FakeRuntimeSettings()).Handle(new GetPaymentSettingsQuery(), TestContext.Current.CancellationToken);

        result.RefundsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_RefundsTurnedOn_ReturnsRefundsEnabled()
    {
        var runtimeSettings = new FakeRuntimeSettings().Set(FeatureFlagRuntimeSettings.RefundsEnabled, true);

        var result = await new GetPaymentSettingsHandler(runtimeSettings).Handle(new GetPaymentSettingsQuery(), TestContext.Current.CancellationToken);

        result.RefundsEnabled.Should().BeTrue();
    }
}
