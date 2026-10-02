using Elmanhg.Application.Shared.Messaging;
using Elmanhg.Infrastructure.Messaging;
using Elmanhg.Infrastructure.OtpDelivery;
using Elmanhg.Tests.Infrastructure.OtpDelivery;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Net;
using System.Text.Json;

namespace Elmanhg.Tests.Infrastructure.Messaging;

public sealed class MetaWhatsAppMessageChannelTests
{
    private const string Phone = "01012345678";
    private static readonly Guid ThreadId = Guid.Parse("6f1c2a52-8d64-4c5e-9b1e-3f7a0d2c9e41");
    private static readonly TeacherThreadReminderMessage Reminder = new(Guid.NewGuid(), Phone, "Mona", ThreadId, "Physics", "Newton's laws", new DateTimeOffset(2026, 12, 1, 10, 0, 0, TimeSpan.Zero));

    private readonly StubHttpMessageHandler _handler = new();
    private readonly FakeLogger<MetaWhatsAppMessageChannel> _logger = new();
    private readonly OutOfAppReminderOptions _reminders = new() { WhatsAppTemplateName = "elmanhg_teacher_reminder", WhatsAppLanguageCode = "ar" };

    [Fact]
    public async Task SendAsync_Reminder_PostsUtilityTemplateWithParametersAndButton()
    {
        var sent = await Channel().SendAsync(Reminder, TestContext.Current.CancellationToken);

        sent.Should().BeTrue();
        _handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        _handler.LastRequest.RequestUri.Should().Be(new Uri("https://graph.facebook.com/v23.0/123456/messages"));
        _handler.LastRequest.Headers.Authorization!.ToString().Should().Be("Bearer meta-token");
        _handler.LastRequest.Headers.UserAgent.ToString().Should().Be("Elmanhg/1.0");
        using var body = JsonDocument.Parse(_handler.LastBody!);
        var root = body.RootElement;
        root.GetProperty("to").GetString().Should().Be("201012345678");
        var template = root.GetProperty("template");
        template.GetProperty("name").GetString().Should().Be("elmanhg_teacher_reminder");
        template.GetProperty("language").GetProperty("code").GetString().Should().Be("ar");
        var components = template.GetProperty("components");
        components[0].GetProperty("type").GetString().Should().Be("body");
        components[0].GetProperty("parameters").EnumerateArray().Select(x => x.GetProperty("text").GetString()).Should().Equal("Physics", "Newton's laws", "2026-12-01 12:00");
        (components[1].GetProperty("type").GetString(), components[1].GetProperty("sub_type").GetString(), components[1].GetProperty("index").GetString()).Should().Be(("button", "url", "0"));
        components[1].GetProperty("parameters")[0].GetProperty("text").GetString().Should().Be(ThreadId.ToString());
    }

    [Fact]
    public async Task SendAsync_ThreadButtonOff_SendsBodyOnly()
    {
        _reminders.WhatsAppThreadButton = false;

        await Channel().SendAsync(Reminder, TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(_handler.LastBody!);
        body.RootElement.GetProperty("template").GetProperty("components").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task SendAsync_ProviderRejects_ReturnsFalse()
    {
        _handler.StatusCode = HttpStatusCode.InternalServerError;
        using var provider = BuildResilientProvider();

        var sent = await provider.GetRequiredService<MetaWhatsAppMessageChannel>().SendAsync(Reminder, TestContext.Current.CancellationToken);

        sent.Should().BeFalse();
        _handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task SendAsync_NetworkFailure_ReturnsFalse()
    {
        _handler.Throw = new HttpRequestException("connection refused");

        var sent = await Channel().SendAsync(Reminder, TestContext.Current.CancellationToken);

        sent.Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_Failure_LogsWithoutPhoneOrToken()
    {
        _handler.StatusCode = HttpStatusCode.InternalServerError;

        await Channel().SendAsync(Reminder, TestContext.Current.CancellationToken);

        var entry = _logger.Collector.GetSnapshot().Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Warning);
        entry.Message.Should().Contain("500").And.NotContain(Phone).And.NotContain("201012345678").And.NotContain("meta-token");
    }

    private MetaWhatsAppMessageChannel Channel() => new(new HttpClient(_handler) { BaseAddress = new Uri("https://graph.facebook.com/") }, Options.Create(OtpDeliveryTestSettings.WithMeta()), Options.Create(_reminders), _logger);

    private ServiceProvider BuildResilientProvider()
    {
        var configuration = OtpDeliveryTestSettings.ToConfiguration(OtpDeliveryTestSettings.WithMeta());
        configuration["OutOfAppReminders:WhatsAppTemplateName"] = _reminders.WhatsAppTemplateName;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<IHostEnvironment>());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(configuration).Build());
        services.AddOtpDelivery();
        services.AddMessaging();
        services.AddHttpClient<MetaWhatsAppMessageChannel>().ConfigurePrimaryHttpMessageHandler(() => _handler);
        services.PostConfigure<HttpStandardResilienceOptions>($"{nameof(MetaWhatsAppMessageChannel)}-standard", resilience => resilience.Retry.Delay = TimeSpan.Zero);
        return services.BuildServiceProvider();
    }
}
