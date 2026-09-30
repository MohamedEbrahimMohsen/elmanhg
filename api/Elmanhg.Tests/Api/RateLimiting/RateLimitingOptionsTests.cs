using Elmanhg.Api.RateLimiting;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Api.RateLimiting;

public sealed class RateLimitingOptionsTests
{
    [Fact]
    public void AddAppRateLimiting_NoConfiguration_BindsSafeDefaults()
    {
        using var provider = Provider([]);

        var options = provider.GetRequiredService<IOptions<RateLimitingOptions>>().Value;

        (options.AuthRefreshPermitLimit, options.AuthRefreshWindowSeconds, options.PublicReadPermitLimit, options.PublicReadWindowSeconds, options.PaymentWebhookPermitLimit, options.PaymentWebhookWindowSeconds, options.AvatarMessagePermitLimit, options.AvatarMessageWindowSeconds, options.AskTeacherSubmissionPermitLimit, options.AskTeacherSubmissionWindowSeconds, options.StudentConcurrentRequestLimit)
            .Should().Be((60, 60, 300, 60, 300, 60, 20, 60, 10, 600, 1));
    }

    [Fact]
    public void AddAppRateLimiting_ZeroPermitLimit_ThrowsOptionsValidationException()
    {
        using var provider = Provider(new Dictionary<string, string?> { ["RateLimiting:AvatarMessagePermitLimit"] = "0" });

        var act = () => provider.GetRequiredService<IOptions<RateLimitingOptions>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    private static ServiceProvider Provider(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddAppRateLimiting();
        return services.BuildServiceProvider();
    }
}
