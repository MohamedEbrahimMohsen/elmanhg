using Elmanhg.Domain.Subscriptions;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
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

public sealed class CheckoutEndpointTests(ApiFactory factory)
{
    private const string CheckoutPath = $"{SubscriptionTestData.SubscriptionsRoute}/checkout";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Post_Anonymous_Returns401()
    {
        using var anonymous = AuthTestClient.Create(factory);

        using var response = await anonymous.PostAsJsonAsync(CheckoutPath, new { plan = "Base", period = "Monthly" }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_Teacher_Returns403()
    {
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, CancellationToken);

        using var response = await client.PostAsJsonAsync(CheckoutPath, new { plan = "Base", period = "Monthly" }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_FreeStudentBaseMonthly_ReturnsFakeRedirectAndStoresPendingPayment()
    {
        var (student, client) = await SignedInStudentAsync(factory);

        using var response = await client.PostAsJsonAsync(CheckoutPath, new { plan = "Base", period = "Monthly" }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        var paymentId = body.GetProperty("paymentId").GetGuid();
        body.GetProperty("redirectUrl").GetString().Should().Be($"{SubscriptionTestData.FakeCheckoutPath}/{paymentId}");
        body.GetProperty("amount").GetProperty("amountMinor").GetInt64().Should().Be(19900);
        using var scope = factory.Services.CreateScope();
        var payment = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Payments.SingleAsync(x => x.Id == paymentId, CancellationToken);
        (payment.Status, payment.Plan, payment.Period, payment.StudentId).Should().Be((PaymentStatus.Pending, SubscriptionPlan.Base, BillingPeriod.Monthly, student.Id));
    }

    [Fact]
    public async Task Post_AskTeacherWithoutBase_Returns400CheckoutRequiresBase()
    {
        var (student, client) = await SignedInStudentAsync(factory);

        using var response = await client.PostAsJsonAsync(CheckoutPath, new { plan = "AskTeacher", period = "Monthly" }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString().Should().Be("CHECKOUT_REQUIRES_BASE");
        using var scope = factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Payments.AnyAsync(x => x.StudentId == student.Id, CancellationToken)).Should().BeFalse();
    }

    [Fact]
    public async Task Post_BaseAlreadyEntitled_Returns400PlanAlreadyActive()
    {
        var (student, client) = await SignedInStudentAsync(factory);
        await SubscriptionTestData.SeedSubscriptionAsync(factory, new SubscriptionBuilder().ForStudent(student.Id).StartingAt(DateTimeOffset.UtcNow.AddDays(-5)).Build(), CancellationToken);

        using var response = await client.PostAsJsonAsync(CheckoutPath, new { plan = "Base", period = "Monthly" }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString().Should().Be("CHECKOUT_PLAN_ALREADY_ACTIVE");
    }

    [Fact]
    public async Task Post_PlanMissing_Returns422WithCode()
    {
        var (_, client) = await SignedInStudentAsync(factory);

        using var response = await client.PostAsJsonAsync(CheckoutPath, new { period = "Monthly" }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString().Should().Contain("CHECKOUT_PLAN_REQUIRED");
    }
}
