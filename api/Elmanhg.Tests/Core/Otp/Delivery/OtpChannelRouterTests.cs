using Core.Errors;
using Core.OTP.Delivery;
using Core.OTP.Entities;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Observability;
using Elmanhg.Tests.Application.Features.Shared.Observability;
using Elmanhg.Tests.Infrastructure.OtpDelivery;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Diagnostics.Metrics;

namespace Elmanhg.Tests.Core.Otp.Delivery;

public sealed class OtpChannelRouterTests
{
    private const string Phone = "01012345678";
    private const string Email = "mona@elmanhg.test";
    private const string Code = "123456";

    private readonly IOtpChannel _whatsApp = Channel(OtpChannel.WhatsApp);
    private readonly IOtpChannel _sms = Channel(OtpChannel.Sms);
    private readonly IOtpChannel _email = Channel(OtpChannel.Email);
    private readonly OtpDeliveryOptions _options = OtpDeliveryTestSettings.Fake();
    private readonly IMeterFactory _meterFactory = MeterFactories.Create();

    [Fact]
    public async Task SendAsync_PhoneWithWhatsAppEnabled_SendsViaWhatsApp()
    {
        _options.Sms.Enabled = true;

        var channel = await Router().SendAsync(OtpRecipientType.Phone, Phone, Code, TestContext.Current.CancellationToken);

        channel.Should().Be(OtpChannel.WhatsApp);
        await _whatsApp.Received(1).SendAsync(Phone, Code, Arg.Any<CancellationToken>());
        await _sms.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_PhoneWithWhatsAppDisabledAndSmsEnabled_FallsBackToSms()
    {
        _options.WhatsApp.Enabled = false;
        _options.Sms.Enabled = true;

        var channel = await Router().SendAsync(OtpRecipientType.Phone, Phone, Code, TestContext.Current.CancellationToken);

        channel.Should().Be(OtpChannel.Sms);
        await _sms.Received(1).SendAsync(Phone, Code, Arg.Any<CancellationToken>());
        await _whatsApp.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_PhoneWithSmsAsDefaultAndBothEnabled_SendsViaSms()
    {
        _options.DefaultPhoneChannel = OtpChannel.Sms;
        _options.Sms.Enabled = true;

        var channel = await Router().SendAsync(OtpRecipientType.Phone, Phone, Code, TestContext.Current.CancellationToken);

        channel.Should().Be(OtpChannel.Sms);
        await _sms.Received(1).SendAsync(Phone, Code, Arg.Any<CancellationToken>());
        await _whatsApp.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_PhoneWithNoPhoneChannelEnabled_ThrowsChannelUnavailable()
    {
        _options.WhatsApp.Enabled = false;

        var act = () => Router().SendAsync(OtpRecipientType.Phone, Phone, Code, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.OtpChannelUnavailable);
        await AssertNoChannelCalledAsync();
    }

    [Fact]
    public async Task SendAsync_EmailWithEmailEnabled_SendsViaEmail()
    {
        var channel = await Router().SendAsync(OtpRecipientType.Email, Email, Code, TestContext.Current.CancellationToken);

        channel.Should().Be(OtpChannel.Email);
        await _email.Received(1).SendAsync(Email, Code, Arg.Any<CancellationToken>());
        await _whatsApp.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_EmailWithEmailDisabled_ThrowsChannelUnavailable()
    {
        _options.Email.Enabled = false;
        _options.Sms.Enabled = true;

        var act = () => Router().SendAsync(OtpRecipientType.Email, Email, Code, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.OtpChannelUnavailable);
        await AssertNoChannelCalledAsync();
    }

    [Fact]
    public async Task SendAsync_ChannelDelivers_RecordsDeliveredSend()
    {
        using var sends = new MetricCollector<long>(_meterFactory, ElmanhgTelemetry.SourceName, "elmanhg.otp.sends");

        await Router().SendAsync(OtpRecipientType.Phone, Phone, Code, TestContext.Current.CancellationToken);

        sends.GetMeasurementSnapshot().Should().ContainSingle().Which.Tags.Should().Contain(ElmanhgMetrics.ChannelTag, "WhatsApp").And.Contain(ElmanhgMetrics.OutcomeTag, ElmanhgMetrics.DeliveredOutcome);
    }

    [Fact]
    public async Task SendAsync_ChannelThrows_RecordsFailedSendAndRethrows()
    {
        using var sends = new MetricCollector<long>(_meterFactory, ElmanhgTelemetry.SourceName, "elmanhg.otp.sends");
        _whatsApp.SendAsync(Phone, Code, Arg.Any<CancellationToken>()).Returns(Task.FromException(new HttpRequestException()));

        var act = () => Router().SendAsync(OtpRecipientType.Phone, Phone, Code, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<HttpRequestException>();
        sends.GetMeasurementSnapshot().Should().ContainSingle().Which.Tags.Should().Contain(ElmanhgMetrics.ChannelTag, "WhatsApp").And.Contain(ElmanhgMetrics.OutcomeTag, ElmanhgMetrics.FailedOutcome);
    }

    [Fact]
    public async Task SendAsync_ChannelDelivers_NotifiesEveryObserver()
    {
        var first = Substitute.For<IOtpDeliveryObserver>();
        var second = Substitute.For<IOtpDeliveryObserver>();
        var router = new OtpChannelRouter(Options.Create(_options), [_whatsApp, _sms, _email], [first, second], OtpDeliveryTestSettings.Setup());

        await router.SendAsync(OtpRecipientType.Phone, Phone, Code, TestContext.Current.CancellationToken);

        first.Received(1).RecordOtpSend(OtpChannel.WhatsApp, true);
        second.Received(1).RecordOtpSend(OtpChannel.WhatsApp, true);
    }

    [Fact]
    public async Task SendAsync_CustomUnavailableCode_ThrowsThatCode()
    {
        _options.WhatsApp.Enabled = false;
        _options.Sms.Enabled = false;
        _options.Email.Enabled = false;
        var router = new OtpChannelRouter(Options.Create(_options), [_whatsApp, _sms, _email], [], OtpDeliveryTestSettings.Setup() with { ChannelUnavailableErrorCode = "NOT_A_REAL_CODE" });

        var act = () => router.SendAsync(OtpRecipientType.Phone, Phone, Code, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be("NOT_A_REAL_CODE");
        await AssertNoChannelCalledAsync();
    }

    private OtpChannelRouter Router() => new(Options.Create(_options), [_whatsApp, _sms, _email], [new ElmanhgMetrics(_meterFactory)], OtpDeliveryTestSettings.Setup());

    private async Task AssertNoChannelCalledAsync()
    {
        await _whatsApp.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _sms.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _email.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static IOtpChannel Channel(OtpChannel channel)
    {
        var substitute = Substitute.For<IOtpChannel>();
        substitute.Channel.Returns(channel);
        return substitute;
    }
}
