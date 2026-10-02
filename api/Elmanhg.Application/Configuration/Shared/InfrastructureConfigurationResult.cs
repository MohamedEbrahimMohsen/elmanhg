namespace Elmanhg.Application.Configuration.Shared;

public sealed record InfrastructureConfigurationResult(string Environment, List<IntegrationProviderResult> Integrations, List<SafetySwitchResult> SafetySwitches, List<SecretStatusResult> Secrets, AiServiceStatus AiServiceStatus, AiServiceConfigurationResult? AiService);

public sealed record IntegrationProviderResult(string Integration, string Provider, IntegrationMode Mode, bool IsEnabled);

public sealed record SafetySwitchResult(string Key, bool IsOn);

public sealed record SecretStatusResult(string Key, bool IsSet);

public sealed record AiServiceConfigurationResult(string LlmProvider, string ChatModel, string EssayGradingModel, string MathStepGradingModel, string EmbeddingProvider, string EmbeddingModel, string TranscriptionProvider, string TranscriptionModel, List<SecretStatusResult> Secrets);
