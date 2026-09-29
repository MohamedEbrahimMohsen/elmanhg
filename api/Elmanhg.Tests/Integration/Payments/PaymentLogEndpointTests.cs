using Elmanhg.Domain.Subscriptions;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.Subscriptions;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.Payments;

public sealed class PaymentLogEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        using var anonymous = AuthTestClient.Create(factory);

        using var response = await anonymous.GetAsync(PaymentsTestData.Route, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Teacher")]
    public async Task Get_NonAdmin_Returns403(string role)
    {
        var user = role == "Student" ? await ScopeTestData.SeedStudentAsync(factory, CancellationToken) : await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, user, CancellationToken);

        using var response = await client.GetAsync(PaymentsTestData.Route, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_Admin_ReturnsPaymentsNewestFirstWithStudentName()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var (succeeded, _) = await PaymentsTestData.SeedSucceededAsync(factory, student.Id, DateTimeOffset.UtcNow, null, CancellationToken);
        var pending = await SubscriptionTestData.SeedPendingPaymentAsync(factory, student.Id, SubscriptionPlan.Base, BillingPeriod.Termly, 69900, CancellationToken);
        using var client = await PaymentsTestData.AdminClientAsync(factory, CancellationToken);

        var items = await GetItemsAsync(client, $"?studentId={student.Id}");

        items.Select(x => (x.GetProperty("id").GetGuid(), x.GetProperty("status").GetString(), x.GetProperty("amount").GetProperty("amountMinor").GetInt64()))
            .Should().Equal((pending.Id, "Pending", 69900L), (succeeded.Id, "Succeeded", 19900L));
        items.Select(x => x.GetProperty("studentName").GetString()).Should().AllBe("Student");
        items.Select(x => x.GetProperty("studentContact").GetString()).Should().AllBe(student.Email);
    }

    [Fact]
    public async Task Get_NeedsReview_ReturnsOnlyOpenReviews()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var (flagged, _) = await PaymentsTestData.SeedSucceededAsync(factory, student.Id, DateTimeOffset.UtcNow, PaymentReviewReason.AskTeacherWithoutBase, CancellationToken);
        await PaymentsTestData.SeedSucceededAsync(factory, student.Id, DateTimeOffset.UtcNow, null, CancellationToken);
        using var client = await PaymentsTestData.AdminClientAsync(factory, CancellationToken);

        var items = await GetItemsAsync(client, $"?studentId={student.Id}&needsReview=true");

        var item = items.Should().ContainSingle().Subject;
        (item.GetProperty("id").GetGuid(), item.GetProperty("needsReview").GetBoolean(), item.GetProperty("reviewReason").GetString()).Should().Be((flagged.Id, true, "AskTeacherWithoutBase"));
    }

    [Fact]
    public async Task Get_StatusAndReferenceFilters_NarrowResults()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var (succeeded, _) = await PaymentsTestData.SeedSucceededAsync(factory, student.Id, DateTimeOffset.UtcNow, null, CancellationToken);
        var failed = await SubscriptionTestData.SeedCompletedPaymentAsync(factory, student.Id, succeeded: false, 19900, CancellationToken);
        using var client = await PaymentsTestData.AdminClientAsync(factory, CancellationToken);

        var byReference = await GetItemsAsync(client, $"?reference={succeeded.PaymobTransactionId}");
        var byStatus = await GetItemsAsync(client, $"?studentId={student.Id}&status=Failed");

        byReference.Should().ContainSingle().Which.GetProperty("id").GetGuid().Should().Be(succeeded.Id);
        byStatus.Should().ContainSingle().Which.GetProperty("id").GetGuid().Should().Be(failed.Id);
    }

    [Fact]
    public async Task Get_PageSizeAboveMax_Returns422WithCode()
    {
        using var client = await PaymentsTestData.AdminClientAsync(factory, CancellationToken);

        using var response = await client.GetAsync($"{PaymentsTestData.Route}?pageSize=101", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString().Should().Contain("PAYMENT_LOG_PAGE_SIZE_INVALID");
    }

    private static async Task<List<JsonElement>> GetItemsAsync(HttpClient client, string query)
    {
        using var response = await client.GetAsync($"{PaymentsTestData.Route}{query}", CancellationToken).ConfigureAwait(false);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken).ConfigureAwait(false);
        return [.. body.GetProperty("items").EnumerateArray().Select(x => x.Clone())];
    }
}
