using Elmanhg.Application.Shared.Email;
using Elmanhg.Application.Shared.Messaging;
using Elmanhg.Domain.Identity;
using Elmanhg.Infrastructure.Invitations;
using Elmanhg.Infrastructure.Messaging;
using Elmanhg.Infrastructure.OtpDelivery;
using Elmanhg.Tests.Infrastructure.OtpDelivery;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using NSubstitute;
using System.Net;

namespace Elmanhg.Tests.Infrastructure.Messaging;

public sealed class ResendRetryTests
{
    private const string Email = "teacher@elmanhg.test";
    private static readonly Guid ThreadId = Guid.Parse("6f1c2a52-8d64-4c5e-9b1e-3f7a0d2c9e41");
    private static readonly Guid UserId = Guid.Parse("0b9d6e3a-4c21-4f7e-8a55-2d1e9c7b3f60");

    private readonly StubHttpMessageHandler _handler = new() { StatusCode = HttpStatusCode.InternalServerError };

    [Fact]
    public async Task InvitationSender_ServerError_RetriesWithTheSameIdempotencyKey()
    {
        using var provider = BuildProvider(nameof(ResendInvitationEmailSender));
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ResendInvitationEmailSender>();

        var sent = await sender.SendAsync(new InvitationEmail(Email, "Mona", UserRole.Teacher), TestContext.Current.CancellationToken);

        sent.Should().BeFalse();
        _handler.CallCount.Should().BeGreaterThan(1);
        _handler.RequestHeaders.Select(x => x["Idempotency-Key"]).Distinct().Should().ContainSingle().Which.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ReminderEmailChannel_ServerError_RetriesWithTheSameIdempotencyKey()
    {
        using var provider = BuildProvider(nameof(ResendEmailMessageChannel));
        using var scope = provider.CreateScope();
        var channel = scope.ServiceProvider.GetRequiredService<ResendEmailMessageChannel>();
        var reminder = new TeacherThreadReminderMessage(UserId, Email, "Mona", ThreadId, "Physics", "Newton's laws", new DateTimeOffset(2026, 12, 1, 10, 0, 0, TimeSpan.Zero));

        var sent = await channel.SendAsync(reminder, TestContext.Current.CancellationToken);

        sent.Should().BeFalse();
        _handler.CallCount.Should().BeGreaterThan(1);
        _handler.RequestHeaders.Select(x => x["Idempotency-Key"]).Distinct().Should().ContainSingle().Which.Should().NotBeNullOrWhiteSpace();
    }

    private ServiceProvider BuildProvider(string clientName)
    {
        var configuration = OtpDeliveryTestSettings.ToConfiguration(OtpDeliveryTestSettings.WithResend());
        configuration["InvitationEmail:AcceptInviteUrl"] = "https://elmanhg.test/accept-invite";
        configuration["OutOfAppReminders:ThreadLinkBaseUrl"] = "https://site.test/teacher/thread/";
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<IHostEnvironment>());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(configuration).Build());
        services.AddOtpDelivery();
        services.AddInvitationEmail();
        services.AddMessaging();
        services.AddHttpClient(clientName).ConfigurePrimaryHttpMessageHandler(() => _handler);
        services.PostConfigure<HttpStandardResilienceOptions>($"{clientName}-standard", resilience => resilience.Retry.Delay = TimeSpan.Zero);
        return services.BuildServiceProvider();
    }
}
