using Elmanhg.Application.Configuration.Shared;
using Elmanhg.Infrastructure.AiService;
using Elmanhg.Infrastructure.OtpDelivery;
using Elmanhg.Infrastructure.Payments;
using Elmanhg.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Elmanhg.Infrastructure.Hosting;

public sealed class InfrastructureConfigurationReader(IOptions<OtpDeliveryOptions> otpDeliveryOptions, IOptions<PaymentsOptions> paymentsOptions, IOptions<FileStorageOptions> fileStorageOptions, IOptions<AiServiceOptions> aiServiceOptions, IConfiguration configuration, IHostEnvironment hostEnvironment, HttpAiConfigurationClient aiConfigurationClient) : IInfrastructureConfigurationReader
{
    public async Task<InfrastructureConfigurationResult> ReadAsync(CancellationToken cancellationToken)
    {
        var payments = paymentsOptions.Value;
        var integrations = InfrastructureIntegrations.Describe(otpDeliveryOptions.Value, payments, fileStorageOptions.Value, aiServiceOptions.Value);
        List<SafetySwitchResult> safetySwitches = [new(PaymentsOptions.AllowFakePaymentsKey, payments.AllowFakePayments)];
        var secrets = ConfigurationSecrets.Keys
            .Select(key => new SecretStatusResult(key, ConfigurationSecrets.IsSet(configuration[key])))
            .ToList();
        var (status, aiService) = await ReadAiServiceAsync(cancellationToken).ConfigureAwait(false);
        return new InfrastructureConfigurationResult(hostEnvironment.EnvironmentName, integrations, safetySwitches, secrets, status, aiService);
    }

    private async Task<(AiServiceStatus Status, AiServiceConfigurationResult? AiService)> ReadAiServiceAsync(CancellationToken cancellationToken)
    {
        if (aiServiceOptions.Value.Provider == AiServiceProvider.Fake)
        {
            return (AiServiceStatus.NotUsed, null);
        }

        var aiService = await aiConfigurationClient.GetAsync(cancellationToken).ConfigureAwait(false);
        return (aiService is null ? AiServiceStatus.Unreachable : AiServiceStatus.Reachable, aiService);
    }
}
