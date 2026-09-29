using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Infrastructure.AiService;
using Elmanhg.Tests.Infrastructure.OtpDelivery;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Net;

namespace Elmanhg.Tests.Infrastructure.AiService;

public sealed class AiServiceServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAiService_NoConfiguration_ResolvesFakeClient()
    {
        using var provider = BuildProvider([]);
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IAiServiceClient>().Should().BeOfType<FakeAiServiceClient>();
    }

    [Fact]
    public void AddAiService_HttpProvider_ResolvesHttpClient()
    {
        using var provider = BuildProvider(AiServiceTestSettings.ToConfiguration(AiServiceTestSettings.WithHttp()));
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IAiServiceClient>().Should().BeOfType<HttpAiServiceClient>();
    }

    [Fact]
    public void AddAiService_HttpWithoutToken_FailsStartupValidation()
    {
        var options = AiServiceTestSettings.WithHttp();
        options.ServiceToken = string.Empty;
        using var provider = BuildProvider(AiServiceTestSettings.ToConfiguration(options));

        var act = () => provider.GetRequiredService<IStartupValidator>().Validate();

        act.Should().Throw<OptionsValidationException>().WithMessage("*ServiceToken*");
    }

    [Fact]
    public async Task AddAiService_HttpServerError_MakesExactlyOneAttempt()
    {
        var handler = new StubHttpMessageHandler { StatusCode = HttpStatusCode.InternalServerError };
        using var provider = BuildProvider(AiServiceTestSettings.ToConfiguration(AiServiceTestSettings.WithHttp()), services =>
        {
            services.AddHttpClient<HttpAiServiceClient>().ConfigurePrimaryHttpMessageHandler(() => handler);
            services.PostConfigure<HttpStandardResilienceOptions>($"{nameof(HttpAiServiceClient)}-standard", resilience => resilience.Retry.Delay = TimeSpan.Zero);
        });
        using var scope = provider.CreateScope();

        var act = () => scope.ServiceProvider.GetRequiredService<IAiServiceClient>().ChatAsync(AiServiceTestSettings.ChatRequest(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.AiServiceUnavailable);
        handler.CallCount.Should().Be(1);
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings, Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<IHostEnvironment>());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        services.AddAiService();
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }
}
