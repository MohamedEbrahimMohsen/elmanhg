using Core.OTP.Delivery;
using Core.OTP.Delivery.WhatsApp;
using Elmanhg.Tests.Infrastructure.OtpDelivery;
using FluentAssertions;

namespace Elmanhg.Tests.Core.Otp.Delivery;

public sealed class OtpDeliveryOptionsValidatorTests
{
    private readonly OtpDeliveryOptionsValidator _validator = new();

    [Fact]
    public void Validate_CommittedFakeDefaults_Succeeds()
    {
        _validator.Validate(null, OtpDeliveryTestSettings.Fake()).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_MetaEnabledWithAllCredentials_Succeeds()
    {
        _validator.Validate(null, OtpDeliveryTestSettings.WithMeta()).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_MetaEnabledWithoutAccessToken_Fails()
    {
        var options = OtpDeliveryTestSettings.WithMeta();
        options.WhatsApp.AccessToken = string.Empty;

        FailureOf(options).Should().Contain("OtpDelivery:WhatsApp:AccessToken");
    }

    [Fact]
    public void Validate_MetaDisabledWithoutCredentials_Succeeds()
    {
        var options = OtpDeliveryTestSettings.Fake();
        options.WhatsApp.Provider = WhatsAppProvider.Meta;
        options.WhatsApp.Enabled = false;

        _validator.Validate(null, options).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_ResendEnabledWithoutApiKey_Fails()
    {
        var options = OtpDeliveryTestSettings.WithResend();
        options.Email.ApiKey = string.Empty;

        FailureOf(options).Should().Contain("OtpDelivery:Email:ApiKey");
    }

    [Fact]
    public void Validate_ResendEnabledWithoutFromAddress_Fails()
    {
        var options = OtpDeliveryTestSettings.WithResend();
        options.Email.FromAddress = string.Empty;

        FailureOf(options).Should().Contain("OtpDelivery:Email:FromAddress");
    }

    [Fact]
    public void Validate_HttpSmsEnabledWithoutUrl_Fails()
    {
        var options = OtpDeliveryTestSettings.WithHttpSms();
        options.Sms.Url = string.Empty;

        FailureOf(options).Should().Contain("OtpDelivery:Sms:Url");
    }

    [Fact]
    public void Validate_HttpSmsBodyWithoutMessageToken_Fails()
    {
        var options = OtpDeliveryTestSettings.WithHttpSms();
        options.Sms.BodyTemplate = "{\"to\":\"{phoneNumber}\"}";

        FailureOf(options).Should().Contain("BodyTemplate must contain {message}");
    }

    [Fact]
    public void Validate_HttpSmsMessageTemplateWithoutCode_Fails()
    {
        var options = OtpDeliveryTestSettings.WithHttpSms();
        options.Sms.MessageTemplate = "Your code";

        FailureOf(options).Should().Contain("MessageTemplate must contain {code}");
    }

    [Fact]
    public void Validate_HttpSmsUnsupportedContentType_Fails()
    {
        var options = OtpDeliveryTestSettings.WithHttpSms();
        options.Sms.ContentType = "text/plain";

        FailureOf(options).Should().Contain("OtpDelivery:Sms:ContentType must be application/json or application/x-www-form-urlencoded.");
    }

    [Fact]
    public void Validate_HttpSmsAuthHeaderNameWithoutValue_Fails()
    {
        var options = OtpDeliveryTestSettings.WithHttpSms();
        options.Sms.AuthHeaderValue = string.Empty;

        FailureOf(options).Should().Contain("must be set together");
    }

    [Fact]
    public void Validate_DefaultPhoneChannelEmail_Fails()
    {
        var options = OtpDeliveryTestSettings.Fake();
        options.DefaultPhoneChannel = OtpChannel.Email;

        FailureOf(options).Should().Contain("OtpDelivery:DefaultPhoneChannel must be WhatsApp or Sms.");
    }

    [Fact]
    public void Validate_AttemptTimeoutAboveTotalTimeout_Fails()
    {
        var options = OtpDeliveryTestSettings.Fake();
        options.AttemptTimeoutSeconds = 15;
        options.TotalTimeoutSeconds = 10;

        FailureOf(options).Should().Contain("OtpDelivery:AttemptTimeoutSeconds must not exceed OtpDelivery:TotalTimeoutSeconds.");
    }

    private string FailureOf(OtpDeliveryOptions options)
    {
        var result = _validator.Validate(null, options);
        result.Failed.Should().BeTrue();
        return result.FailureMessage ?? string.Empty;
    }
}
