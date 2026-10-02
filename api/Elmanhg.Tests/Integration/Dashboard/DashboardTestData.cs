using Elmanhg.Domain.Analytics;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.Dashboard;

public static class DashboardTestData
{
    public const string Route = "/api/dashboard";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public static async Task<HttpClient> AdminClientAsync(ApiFactory factory)
    {
        var admin = await ScopeTestData.SeedAdminAsync(factory, CancellationToken).ConfigureAwait(false);
        return await ScopeTestData.SignedInClientAsync(factory, admin, CancellationToken).ConfigureAwait(false);
    }

    public static async Task<JsonElement> GetJsonAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync($"{Route}/{path}", CancellationToken).ConfigureAwait(false);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken).ConfigureAwait(false);
    }

    public static Task SeedActivityAsync(ApiFactory factory, Guid userId, DateOnly day) => SaveAsync(factory, context => context.UserActivityDays.Add(UserActivityDay.Record(userId, day, new DateTimeOffset(day.ToDateTime(new TimeOnly(10, 0)), TimeSpan.Zero))));

    public static async Task SetCreationDateAsync(ApiFactory factory, Guid userId, DateTimeOffset creationDate)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Users.Where(x => x.Id == userId).ExecuteUpdateAsync(setters => setters.SetProperty(x => x.CreationDate, creationDate), CancellationToken).ConfigureAwait(false);
    }

    public static Task SeedFunnelEventAsync(ApiFactory factory, Guid anonymousId, FunnelEventType type, DateTimeOffset occurredAt) => SaveAsync(factory, context => context.FunnelEvents.Add(FunnelEvent.Record(anonymousId, null, type, occurredAt)));

    public static Task SeedBreachAsync(ApiFactory factory, TeacherThread thread, DateTimeOffset occurredAt) => SaveAsync(factory, context => context.TeacherThreadSlaEvents.Add(TeacherThreadSlaEvent.Record(thread.Id, TeacherThreadSlaEventKind.Breach, thread.SlaWindowStartedAt, thread.SlaDueAt, null, occurredAt)));

    public static async Task<Payment> SeedPaymentAsync(ApiFactory factory, Guid studentId, BillingPeriod period, int periodMonths, long amountMinor, PaymentStatus status, DateTimeOffset completedAt, DateTimeOffset? refundedAt = null)
    {
        var payment = Payment.Create(studentId, SubscriptionPlan.Base, period, periodMonths, new Money(amountMinor, "EGP"));
        var subscription = Subscription.Start(studentId, SubscriptionPlan.Base, period, periodMonths, completedAt, null, studentId);
        if (status == PaymentStatus.Failed)
        {
            payment.MarkFailed($"txn-{Guid.NewGuid():N}", "{}", completedAt);
        }
        else
        {
            payment.MarkSucceeded(subscription.Id, $"txn-{Guid.NewGuid():N}", "{}", completedAt);
        }

        if (refundedAt is { } refunded)
        {
            payment.MarkRefunded($"refund-{Guid.NewGuid():N}", refunded, null, "Requested", null);
        }

        await SaveAsync(factory, context =>
        {
            if (payment.SubscriptionId is not null)
            {
                context.Subscriptions.Add(subscription);
            }

            context.Payments.Add(payment);
        }).ConfigureAwait(false);
        return payment;
    }

    public static async Task<Guid> SubjectOfLessonAsync(ApiFactory factory, Guid lessonId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.Lessons.Where(x => x.Id == lessonId).Join(context.Units, lesson => lesson.UnitId, unit => unit.Id, (lesson, unit) => unit.SubjectId).SingleAsync(CancellationToken).ConfigureAwait(false);
    }

    private static async Task SaveAsync(ApiFactory factory, Action<AppDbContext> add)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        add(context);
        await context.SaveChangesAsync(CancellationToken).ConfigureAwait(false);
    }
}
