using Elmanhg.Domain.Subscriptions;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.Payments;

public sealed class ResolvePaymentReviewEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Post_FlaggedPayment_ClosesReviewAndLeavesQueue()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var (payment, _) = await PaymentsTestData.SeedSucceededAsync(factory, student.Id, DateTimeOffset.UtcNow, PaymentReviewReason.AskTeacherWithoutBase, CancellationToken);
        using var admin = await PaymentsTestData.AdminClientAsync(factory, CancellationToken);

        using var response = await admin.PostAsync(ResolveRoute(payment.Id), null, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        (body.GetProperty("needsReview").GetBoolean(), body.GetProperty("status").GetString()).Should().Be((false, "Succeeded"));
        using var queue = await admin.GetAsync($"{PaymentsTestData.Route}?studentId={student.Id}&needsReview=true", CancellationToken);
        (await queue.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Post_NotFlagged_Returns400ReviewNotOpen()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var (payment, _) = await PaymentsTestData.SeedSucceededAsync(factory, student.Id, DateTimeOffset.UtcNow, null, CancellationToken);
        using var admin = await PaymentsTestData.AdminClientAsync(factory, CancellationToken);

        using var response = await admin.PostAsync(ResolveRoute(payment.Id), null, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString().Should().Be("PAYMENT_REVIEW_NOT_OPEN");
    }

    [Fact]
    public async Task Post_Student_Returns403()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, student, CancellationToken);

        using var response = await client.PostAsync(ResolveRoute(Guid.NewGuid()), null, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static string ResolveRoute(Guid paymentId) => $"{PaymentsTestData.Route}/{paymentId}/review-resolution";
}
