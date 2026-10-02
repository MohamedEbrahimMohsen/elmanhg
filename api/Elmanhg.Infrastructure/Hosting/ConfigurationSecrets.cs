namespace Elmanhg.Infrastructure.Hosting;

public static class ConfigurationSecrets
{
    // Every secret in the committed deploy/*.env.example files starts with this marker.
    public const string PlaceholderPrefix = "change-me";

    public static readonly IReadOnlyList<string> Keys = ["CoreJwt:Key", "CoreOtp:Secret", "AdminSeed:Password", "TrainingData:StudentIdHashKey", "AiService:ServiceToken", "Payments:Paymob:SecretKey", "Payments:Paymob:HmacSecret", "OtpDelivery:WhatsApp:AccessToken", "OtpDelivery:Email:ApiKey", "OtpDelivery:Sms:AuthHeaderValue", "FileStorage:S3SecretAccessKey"];

    public static bool IsPlaceholder(string? value) => value?.Trim().StartsWith(PlaceholderPrefix, StringComparison.OrdinalIgnoreCase) == true;

    public static bool IsSet(string? value) => !string.IsNullOrWhiteSpace(value) && !IsPlaceholder(value);
}
