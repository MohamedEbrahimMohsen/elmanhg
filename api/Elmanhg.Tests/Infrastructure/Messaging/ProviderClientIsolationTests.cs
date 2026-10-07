using Core.OTP;
using Core.OTP.Delivery.WhatsApp;
using Elmanhg.Application.Shared.Messaging;
using Elmanhg.Infrastructure.Messaging;
using Elmanhg.Infrastructure.OtpDelivery;
using Elmanhg.Tests.Infrastructure.OtpDelivery;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using System.Net;

namespace Elmanhg.Tests.Infrastructure.Messaging;

public sealed class ProviderClientIsolationTests
{
    private const int ReminderBurst = 120;
    private static readonly TeacherThreadReminderMessage Reminder = new(Guid.NewGuid(), "01012345678", "Mona", Guid.NewGuid(), "Physics", "Newton's laws", new DateTimeOffset(2026, 12, 1, 10, 0, 0, TimeSpan.Zero));

    private readonly StubHttpMessageHandler _reminderHandler = new() { StatusCode = HttpStatusCode.InternalServerError };
    private readonly StubHttpMessageHandler _otpHandler = new();

    [Fact]
    public async Task ReminderWhatsAppBreakerOpen_OtpWhatsAppStillReachesProvider()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var reminders = scope.ServiceProvider.GetRequiredService<MetaWhatsAppMessageChannel>();
        for (var i = 0; i < ReminderBurst; i++)
        {
            await reminders.SendAsync(Reminder, TestContext.Current.CancellationToken);
        }

        await scope.ServiceProvider.GetRequiredService<MetaWhatsAppOtpChannel>().SendAsync("01012345678", "482913", TestContext.Current.CancellationToken);

        _reminderHandler.CallCount.Should().BeLessThan(ReminderBurst);
        _otpHandler.CallCount.Should().Be(1);
    }

    private ServiceProvider BuildProvider()
    {
        var configuration = OtpDeliveryTestSettings.ToConfiguration(OtpDeliveryTestSettings.WithMeta());
        configuration["OutOfAppReminders:WhatsAppTemplateName"] = "elmanhg_teacher_reminder";
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<IHostEnvironment>());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(configuration).Build());
        services.AddOptions<OtpOptions>();
        services.AddOtpDelivery();
        services.AddMessaging();
        services.AddHttpClient(nameof(MetaWhatsAppMessageChannel)).ConfigurePrimaryHttpMessageHandler(() => _reminderHandler);
        services.AddHttpClient(nameof(MetaWhatsAppOtpChannel)).ConfigurePrimaryHttpMessageHandler(() => _otpHandler);
        return services.BuildServiceProvider();
    }
}
