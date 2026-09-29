using Elmanhg.Domain.Subscriptions;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Subscriptions;

public sealed class FakePaymentCompletionEndpointTests(ApiFactory factory)
{
    private const string EntitlementPath = $"{SubscriptionTestData.SubscriptionsRoute}/entitlement";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Post_Anonymous_Returns401()
    {
        using var anonymous = AuthTestClient.Create(factory);

        using var response = await anonymous.PostAsJsonAsync(CompletionPath(Guid.NewGuid()), new { succeeded = true }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_Succeeded_ActivatesBase()
    {
        var (student, client) = await SignedInStudentAsync(factory);
        var payment = await SeedPendingAsync(student.Id);

        using var response = await client.PostAsJsonAsync(CompletionPath(payment.Id), new { succeeded = true }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("status").GetString().Should().Be("Succeeded");
        (await GetTierAsync(client)).Should().Be("Base");
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var subscription = await context.Subscriptions.SingleAsync(x => x.StudentId == student.Id, CancellationToken);
        (subscription.Plan, subscription.Status).Should().Be((SubscriptionPlan.Base, SubscriptionStatus.Active));
        (await context.Payments.SingleAsync(x => x.Id == payment.Id, CancellationToken)).SubscriptionId.Should().Be(subscription.Id);
    }

    [Fact]
    public async Task Post_Failed_MarksFailedAndStudentStaysFree()
    {
        var (student, client) = await SignedInStudentAsync(factory);
        var payment = await SeedPendingAsync(student.Id);

        using var response = await client.PostAsJsonAsync(CompletionPath(payment.Id), new { succeeded = false }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("status").GetString().Should().Be("Failed");
        (await GetTierAsync(client)).Should().Be("Free");
        using var scope = factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Subscriptions.AnyAsync(x => x.StudentId == student.Id, CancellationToken)).Should().BeFalse();
    }

    [Fact]
    public async Task Post_AlreadyCompleted_Returns400PaymentNotPending()
    {
        var (student, client) = await SignedInStudentAsync(factory);
        var payment = await SeedPendingAsync(student.Id);
        using var first = await client.PostAsJsonAsync(CompletionPath(payment.Id), new { succeeded = false }, CancellationToken);

        using var response = await client.PostAsJsonAsync(CompletionPath(payment.Id), new { succeeded = true }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString().Should().Be("PAYMENT_NOT_PENDING");
    }

    [Fact]
    public async Task Post_OtherStudentsPayment_Returns404PaymentNotFound()
    {
        var other = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var payment = await SeedPendingAsync(other.Id);
        var (_, client) = await SignedInStudentAsync(factory);

        using var response = await client.PostAsJsonAsync(CompletionPath(payment.Id), new { succeeded = true }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString().Should().Be("PAYMENT_NOT_FOUND");
        using var scope = factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Payments.SingleAsync(x => x.Id == payment.Id, CancellationToken)).Status.Should().Be(PaymentStatus.Pending);
    }

    private Task<Payment> SeedPendingAsync(Guid studentId) => SubscriptionTestData.SeedPendingPaymentAsync(factory, studentId, SubscriptionPlan.Base, BillingPeriod.Monthly, 19900, CancellationToken);

    private static string CompletionPath(Guid paymentId) => $"{SubscriptionTestData.SubscriptionsRoute}/payments/{paymentId}/fake-completion";

    private static async Task<string?> GetTierAsync(HttpClient client)
    {
        using var response = await client.GetAsync(EntitlementPath, CancellationToken).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken).ConfigureAwait(false)).GetProperty("tier").GetString();
    }
}
