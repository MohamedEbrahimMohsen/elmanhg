using Elmanhg.Application.Shared.RuntimeSettings;
using Elmanhg.Application.Shared.RuntimeSettings.Definitions;
using MediatR;

namespace Elmanhg.Application.Payments.GetPaymentSettings;

public sealed class GetPaymentSettingsHandler(IRuntimeSettings runtimeSettings) : IRequestHandler<GetPaymentSettingsQuery, PaymentSettingsResult>
{
    public async Task<PaymentSettingsResult> Handle(GetPaymentSettingsQuery request, CancellationToken cancellationToken)
    {
        var refundsEnabled = await runtimeSettings.GetAsync(FeatureFlagRuntimeSettings.RefundsEnabled, cancellationToken).ConfigureAwait(false);
        return new PaymentSettingsResult(refundsEnabled);
    }
}
