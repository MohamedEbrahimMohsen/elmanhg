using Elmanhg.Application.Shared.Messaging;
using Elmanhg.Infrastructure.Messaging;
using Elmanhg.Tests.Infrastructure.OtpDelivery;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;

namespace Elmanhg.Tests.Infrastructure.Messaging;

public sealed class ResendEmailMessageChannelTests
{
    private const string Email = "teacher@elmanhg.test";
    private static readonly Guid ThreadId = Guid.Parse("6f1c2a52-8d64-4c5e-9b1e-3f7a0d2c9e41");
    private static readonly Guid UserId = Guid.Parse("0b9d6e3a-4c21-4f7e-8a55-2d1e9c7b3f60");
    private static readonly TeacherThreadReminderMessage Reminder = new(UserId, Email, "Mona", ThreadId, "Physics", "Newton's laws", new DateTimeOffset(2026, 12, 1, 10, 0, 0, TimeSpan.Zero));

    private readonly StubHttpMessageHandler _handler = new();
    private readonly OutOfAppReminderOptions _reminders = new() { ThreadLinkBaseUrl = "https://site.test/teacher/thread/" };

    [Fact]
    public async Task SendAsync_ArabicReminder_PostsArabicSubjectBodyAndLink()
    {
        var sent = await Channel().SendAsync(Reminder, TestContext.Current.CancellationToken);

        sent.Should().BeTrue();
        _handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        _handler.LastRequest.RequestUri.Should().Be(new Uri("https://api.resend.com/emails"));
        _handler.LastRequest.Headers.Authorization!.ToString().Should().Be("Bearer re_test");
        using var body = JsonDocument.Parse(_handler.LastBody!);
        var root = body.RootElement;
        root.GetProperty("to").EnumerateArray().Select(x => x.GetString()).Should().Equal(Email);
        root.GetProperty("subject").GetString().Should().Be("تذكير: سؤال طالب بانتظار ردك");
        var link = $"https://site.test/teacher/thread/{ThreadId}";
        root.GetProperty("html").GetString().Should().Contain("dir=\"rtl\"").And.Contain("Physics").And.Contain("2026-12-01 12:00").And.Contain($"href=\"{link}\"");
        root.GetProperty("text").GetString().Should().Contain(link);
    }

    [Fact]
    public async Task SendAsync_EnglishLanguage_UsesEnglishTemplateAndSubject()
    {
        _reminders.EmailLanguage = "en";

        await Channel().SendAsync(Reminder, TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(_handler.LastBody!);
        body.RootElement.GetProperty("subject").GetString().Should().Be("Reminder: a student question is waiting for your reply");
        body.RootElement.GetProperty("html").GetString().Should().Contain("lang=\"en\"").And.Contain("Open the question");
        body.RootElement.GetProperty("text").GetString().Should().Contain("Reply by: 2026-12-01 12:00");
    }

    [Fact]
    public async Task SendAsync_Reminder_UsesDeterministicIdempotencyKey()
    {
        await Channel().SendAsync(Reminder, TestContext.Current.CancellationToken);

        _handler.LastRequest!.Headers.GetValues("Idempotency-Key").Single().Should().Be($"teacher-reminder-{ThreadId:N}-{UserId:N}");
    }

    [Fact]
    public async Task SendAsync_HtmlEncodesValues()
    {
        await Channel().SendAsync(Reminder with { SubjectName = "<b>Physics</b>" }, TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(_handler.LastBody!);
        body.RootElement.GetProperty("html").GetString().Should().Contain("&lt;b&gt;Physics&lt;/b&gt;").And.NotContain("<b>Physics</b>");
        body.RootElement.GetProperty("text").GetString().Should().Contain("<b>Physics</b>");
    }

    [Fact]
    public async Task SendAsync_ProviderRejects_ReturnsFalse()
    {
        _handler.StatusCode = HttpStatusCode.UnprocessableEntity;

        var sent = await Channel().SendAsync(Reminder, TestContext.Current.CancellationToken);

        sent.Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_NetworkFailure_ReturnsFalse()
    {
        _handler.Throw = new HttpRequestException("connection refused");

        var sent = await Channel().SendAsync(Reminder, TestContext.Current.CancellationToken);

        sent.Should().BeFalse();
    }

    private ResendEmailMessageChannel Channel() => new(new HttpClient(_handler) { BaseAddress = new Uri("https://api.resend.com/") }, Options.Create(OtpDeliveryTestSettings.WithResend()), Options.Create(_reminders), NullLogger<ResendEmailMessageChannel>.Instance);
}
