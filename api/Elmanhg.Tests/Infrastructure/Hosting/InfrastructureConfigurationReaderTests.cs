using Elmanhg.Application.Configuration.Shared;
using Elmanhg.Infrastructure.AiService;
using Elmanhg.Infrastructure.Hosting;
using Elmanhg.Infrastructure.OtpDelivery;
using Elmanhg.Infrastructure.OtpDelivery.Email;
using Elmanhg.Infrastructure.OtpDelivery.Sms;
using Elmanhg.Infrastructure.OtpDelivery.WhatsApp;
using Elmanhg.Infrastructure.Payments;
using Elmanhg.Infrastructure.Storage;
using Elmanhg.Tests.Infrastructure.AiService;
using Elmanhg.Tests.Infrastructure.OtpDelivery;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.Net;
using System.Text.Json;

namespace Elmanhg.Tests.Infrastructure.Hosting;

public sealed class InfrastructureConfigurationReaderTests
{
    private const string RealSecret = "not-a-secret-config-probe-7f3a91c2";
    private readonly StubHttpMessageHandler _handler = new() { ResponseBody = HttpAiConfigurationClientTests.ReplyBody };
    private readonly OtpDeliveryOptions _otpDelivery = new() { WhatsApp = new WhatsAppOtpOptions { Enabled = true }, Email = new EmailOtpOptions { Enabled = true }, Sms = new SmsOtpOptions { Enabled = true } };
    private readonly PaymentsOptions _payments = new();
    private readonly FileStorageOptions _fileStorage = new();
    private AiServiceOptions _aiService = AiServiceTestSettings.Fake();
    private Dictionary<string, string?> _configuration = [];

    [Fact]
    public async Task ReadAsync_FakeDefaults_ReportsFakeLocalAndAiNotUsed()
    {
        var result = await ReadAsync();

        result.Integrations.Select(x => (x.Integration, x.Mode)).Should().Equal(("otpWhatsApp", IntegrationMode.Fake), ("otpEmail", IntegrationMode.Fake), ("otpSms", IntegrationMode.Fake), ("invitationEmail", IntegrationMode.Fake), ("payments", IntegrationMode.Fake), ("fileStorage", IntegrationMode.Local), ("aiService", IntegrationMode.Fake));
        (result.AiServiceStatus, result.AiService).Should().Be((AiServiceStatus.NotUsed, (AiServiceConfigurationResult?)null));
        _handler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task ReadAsync_RealProviders_ReportsReal()
    {
        _otpDelivery.WhatsApp.Provider = WhatsAppProvider.Meta;
        _otpDelivery.Email.Provider = EmailProvider.Resend;
        _otpDelivery.Sms.Provider = SmsProvider.Http;
        _payments.Provider = PaymentProvider.Paymob;
        _fileStorage.Provider = FileStorageProvider.S3;
        _aiService = AiServiceTestSettings.WithHttp();

        var result = await ReadAsync();

        result.Integrations.Should().OnlyContain(x => x.Mode == IntegrationMode.Real);
        result.AiServiceStatus.Should().Be(AiServiceStatus.Reachable);
        result.AiService!.ChatModel.Should().Be("gpt-5.6-luna");
    }

    [Fact]
    public async Task ReadAsync_SmsDisabled_ReportsFakeAndDisabled()
    {
        _otpDelivery.Sms.Enabled = false;
        _otpDelivery.Sms.Provider = SmsProvider.Http;

        var result = await ReadAsync();

        var sms = result.Integrations.Single(x => x.Integration == "otpSms");
        (sms.Mode, sms.IsEnabled).Should().Be((IntegrationMode.Fake, false));
    }

    [Fact]
    public async Task ReadAsync_Secrets_SetOnlyForRealValues()
    {
        _configuration = new() { ["Payments:Paymob:SecretKey"] = RealSecret, ["Payments:Paymob:HmacSecret"] = "change-me-x" };

        var result = await ReadAsync();

        var secrets = result.Secrets.ToDictionary(x => x.Key, x => x.IsSet);
        (secrets["Payments:Paymob:SecretKey"], secrets["Payments:Paymob:HmacSecret"], secrets["CoreJwt:Key"]).Should().Be((true, false, false));
        JsonSerializer.Serialize(result).Should().NotContain(RealSecret);
    }

    [Fact]
    public async Task ReadAsync_AiUnreachable_ReportsUnreachable()
    {
        _aiService = AiServiceTestSettings.WithHttp();
        _handler.StatusCode = HttpStatusCode.ServiceUnavailable;

        var result = await ReadAsync();

        (result.AiServiceStatus, result.AiService).Should().Be((AiServiceStatus.Unreachable, (AiServiceConfigurationResult?)null));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReadAsync_AllowFakePayments_IsReportedAsSafetySwitch(bool allowFakePayments)
    {
        _payments.AllowFakePayments = allowFakePayments;

        var result = await ReadAsync();

        result.SafetySwitches.Should().Equal(new SafetySwitchResult("Payments:AllowFakePayments", allowFakePayments));
    }

    private Task<InfrastructureConfigurationResult> ReadAsync()
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns("Testing");
        var aiOptions = Microsoft.Extensions.Options.Options.Create(_aiService);
        var client = new HttpAiConfigurationClient(new HttpClient(_handler) { BaseAddress = new Uri("http://ai.test/") }, aiOptions, NullLogger<HttpAiConfigurationClient>.Instance);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(_configuration).Build();
        var reader = new InfrastructureConfigurationReader(Microsoft.Extensions.Options.Options.Create(_otpDelivery), Microsoft.Extensions.Options.Options.Create(_payments), Microsoft.Extensions.Options.Options.Create(_fileStorage), aiOptions, configuration, environment, client);
        return reader.ReadAsync(TestContext.Current.CancellationToken);
    }
}
