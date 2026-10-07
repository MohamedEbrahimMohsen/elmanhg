using Core.Messaging.Email;
using Elmanhg.Application.Shared.Email;
using Elmanhg.Domain.Identity;
using Elmanhg.Infrastructure.Invitations;
using Elmanhg.Tests.Core.Http;
using Elmanhg.Tests.Infrastructure.OtpDelivery;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;

namespace Elmanhg.Tests.Infrastructure.Invitations;

public sealed class ResendInvitationEmailSenderTests
{
    private const string Email = "teacher@elmanhg.test";
    private const string Link = "https://elmanhg.test/accept-invite";

    private readonly StubHttpMessageHandler _handler = new();

    [Fact]
    public async Task SendAsync_Accepted_PostsArabicInvitationWithLinkAndReturnsTrue()
    {
        var sent = await Sender().SendAsync(new InvitationEmail(Email, "Mona <b>", UserRole.Teacher), TestContext.Current.CancellationToken);

        sent.Should().BeTrue();
        _handler.LastRequest!.RequestUri.Should().Be(new Uri("https://api.resend.com/emails"));
        _handler.LastRequest.Headers.Authorization!.ToString().Should().Be("Bearer re_test");
        _handler.LastRequest.Headers.GetValues("Idempotency-Key").Single().Should().NotBeNullOrWhiteSpace();
        using var body = JsonDocument.Parse(_handler.LastBody!);
        var root = body.RootElement;
        root.GetProperty("to").EnumerateArray().Select(x => x.GetString()).Should().Equal(Email);
        root.GetProperty("subject").GetString().Should().Be("Elmanhg invitation");
        root.GetProperty("html").GetString().Should().Contain("dir=\"rtl\"").And.Contain($"href=\"{Link}\"").And.Contain("Mona &lt;b&gt;").And.Contain("معلّمًا");
        root.GetProperty("text").GetString().Should().Contain(Link);
    }

    [Fact]
    public async Task SendAsync_ProviderRejects_ReturnsFalse()
    {
        _handler.StatusCode = HttpStatusCode.UnprocessableEntity;

        var sent = await Sender().SendAsync(new InvitationEmail(Email, "Mona", UserRole.Admin), TestContext.Current.CancellationToken);

        sent.Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_NetworkFailure_ReturnsFalse()
    {
        _handler.Throw = new HttpRequestException("connection refused");

        var sent = await Sender().SendAsync(new InvitationEmail(Email, "Mona", UserRole.Teacher), TestContext.Current.CancellationToken);

        sent.Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_NonTransientFailure_Throws()
    {
        _handler.Throw = new InvalidOperationException("unexpected provider fault");

        var act = () => Sender().SendAsync(new InvitationEmail(Email, "Mona", UserRole.Teacher), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private ResendInvitationEmailSender Sender() => new(new ResendEmailClient(new HttpClient(_handler) { BaseAddress = new Uri("https://api.resend.com/") }, CoreHttpTestSettings.Create()), Options.Create(OtpDeliveryTestSettings.WithResend()), Options.Create(new InvitationEmailOptions { AcceptInviteUrl = Link, Subject = "Elmanhg invitation" }), NullLogger<ResendInvitationEmailSender>.Instance);
}
