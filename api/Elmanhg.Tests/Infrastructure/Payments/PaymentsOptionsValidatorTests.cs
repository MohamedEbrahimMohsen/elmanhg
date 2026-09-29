using Elmanhg.Infrastructure.Payments;
using Elmanhg.Infrastructure.Payments.Paymob;
using FluentAssertions;

namespace Elmanhg.Tests.Infrastructure.Payments;

public sealed class PaymentsOptionsValidatorTests
{
    private readonly PaymentsOptionsValidator _validator = new();

    [Fact]
    public void Validate_FakeProviderWithBlankPaymobSettings_Succeeds()
    {
        var result = _validator.Validate(null, PaymentsTestSettings.Fake());

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_CompletePaymobSettings_Succeeds()
    {
        var result = _validator.Validate(null, PaymentsTestSettings.WithPaymob());

        result.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData("SecretKey")]
    [InlineData("PublicKey")]
    [InlineData("RedirectionUrl")]
    [InlineData("BillingCountry")]
    public void Validate_PaymobRequiredValueBlank_FailsNamingKey(string key)
    {
        var options = PaymentsTestSettings.WithPaymob();
        Set(options.Paymob, key, " ");

        var result = _validator.Validate(null, options);

        result.Failures.Should().Contain(x => x.Contains($"Payments:Paymob:{key} is required", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_PaymobWithoutIntegrationIds_Fails()
    {
        var options = PaymentsTestSettings.WithPaymob();
        options.Paymob.IntegrationIds = [];

        var result = _validator.Validate(null, options);

        result.Failures.Should().Contain("Payments:Paymob:IntegrationIds needs at least one positive integration id.");
    }

    [Fact]
    public void Validate_PaymobNonPositiveIntegrationId_Fails()
    {
        var options = PaymentsTestSettings.WithPaymob();
        options.Paymob.IntegrationIds = [0];

        var result = _validator.Validate(null, options);

        result.Failures.Should().Contain("Payments:Paymob:IntegrationIds needs at least one positive integration id.");
    }

    [Theory]
    [InlineData("BaseUrl")]
    [InlineData("CheckoutUrl")]
    [InlineData("RedirectionUrl")]
    [InlineData("NotificationUrl")]
    public void Validate_PaymobUrlNotHttps_FailsRequiringHttps(string key)
    {
        var options = PaymentsTestSettings.WithPaymob();
        Set(options.Paymob, key, "http://insecure.test/path");

        var result = _validator.Validate(null, options);

        result.Failures.Should().Contain($"Payments:Paymob:{key} must be an absolute https URL.");
    }

    [Fact]
    public void Validate_AttemptTimeoutAboveTotal_Fails()
    {
        var options = PaymentsTestSettings.Fake();
        options.AttemptTimeoutSeconds = 15;
        options.TotalTimeoutSeconds = 10;

        var result = _validator.Validate(null, options);

        result.Failures.Should().Contain("Payments:AttemptTimeoutSeconds must not exceed Payments:TotalTimeoutSeconds.");
    }

    private static void Set(PaymobOptions paymob, string key, string value)
    {
        Action set = key switch
        {
            nameof(PaymobOptions.SecretKey) => () => paymob.SecretKey = value,
            nameof(PaymobOptions.PublicKey) => () => paymob.PublicKey = value,
            nameof(PaymobOptions.RedirectionUrl) => () => paymob.RedirectionUrl = value,
            nameof(PaymobOptions.BillingCountry) => () => paymob.BillingCountry = value,
            nameof(PaymobOptions.BaseUrl) => () => paymob.BaseUrl = value,
            nameof(PaymobOptions.CheckoutUrl) => () => paymob.CheckoutUrl = value,
            nameof(PaymobOptions.NotificationUrl) => () => paymob.NotificationUrl = value,
            _ => throw new ArgumentOutOfRangeException(nameof(key)),
        };
        set();
    }
}
