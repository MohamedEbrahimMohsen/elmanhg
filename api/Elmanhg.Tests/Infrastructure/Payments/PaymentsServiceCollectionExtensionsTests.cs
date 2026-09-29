using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Payments;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Infrastructure.Payments;
using Elmanhg.Infrastructure.Payments.Paymob;
using Elmanhg.Tests.Infrastructure.OtpDelivery;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Net;

namespace Elmanhg.Tests.Infrastructure.Payments;

public sealed class PaymentsServiceCollectionExtensionsTests
{
    [Fact]
    public void AddPayments_NoConfiguration_ResolvesFakeGateway()
    {
        using var provider = BuildProvider([]);
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IPaymentGateway>().Should().BeOfType<FakePaymentGateway>();
    }

    [Fact]
    public void AddPayments_PaymobProvider_ResolvesPaymobGateway()
    {
        using var provider = BuildProvider(PaymentsTestSettings.ToConfiguration(PaymentsTestSettings.WithPaymob()));
        using var scope = provider.CreateScope();

        var gateway = scope.ServiceProvider.GetRequiredService<IPaymentGateway>();

        gateway.Should().BeOfType<PaymobPaymentGateway>();
        gateway.SupportsSimulatedCompletion.Should().BeFalse();
    }

    [Fact]
    public void AddPayments_PaymobWithoutSecretKey_FailsStartupValidation()
    {
        var options = PaymentsTestSettings.WithPaymob();
        options.Paymob.SecretKey = string.Empty;
        using var provider = BuildProvider(PaymentsTestSettings.ToConfiguration(options));

        var act = () => provider.GetRequiredService<IStartupValidator>().Validate();

        act.Should().Throw<OptionsValidationException>().WithMessage("*SecretKey*");
    }

    [Fact]
    public async Task AddPayments_PaymobServerError_MakesExactlyOneAttempt()
    {
        var handler = new StubHttpMessageHandler { StatusCode = HttpStatusCode.InternalServerError };
        using var provider = BuildProvider(PaymentsTestSettings.ToConfiguration(PaymentsTestSettings.WithPaymob()), services =>
        {
            services.AddHttpClient<PaymobPaymentGateway>().ConfigurePrimaryHttpMessageHandler(() => handler);
            services.PostConfigure<HttpStandardResilienceOptions>($"{nameof(PaymobPaymentGateway)}-standard", resilience => resilience.Retry.Delay = TimeSpan.Zero);
        });
        using var scope = provider.CreateScope();
        var request = new PaymentCheckoutRequest(Guid.NewGuid(), new Money(19900, "EGP"), SubscriptionPlan.Base, BillingPeriod.Monthly, new PaymentCustomer("Mona Ali", null, "01012345678"));

        var act = () => scope.ServiceProvider.GetRequiredService<IPaymentGateway>().StartCheckoutAsync(request, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.PaymentGatewayUnavailable);
        handler.CallCount.Should().Be(1);
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings, Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<IHostEnvironment>());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        services.AddPayments();
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }
}
