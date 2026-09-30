using Elmanhg.Domain.Subscriptions;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.Users;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace Elmanhg.Tests.Integration.Subscriptions;

public sealed class ComplimentarySubscriptionEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Grant_BaseToFreeStudent_EntitlesStudentAndAudits()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var adminUser = await ScopeTestData.SeedAdminAsync(factory, CancellationToken);
        using var admin = await ScopeTestData.SignedInClientAsync(factory, adminUser, CancellationToken);

        using var response = await GrantAsync(admin, student.Id, "Base", "Monthly");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await UsersTestData.ReadBodyAsync(response, CancellationToken);
        body.GetProperty("isComplimentary").GetBoolean().Should().BeTrue();
        var subscriptionId = body.GetProperty("id").GetGuid();
        using var studentClient = await ScopeTestData.SignedInClientAsync(factory, student, CancellationToken);
        using var entitlement = await studentClient.GetAsync($"{SubscriptionTestData.SubscriptionsRoute}/entitlement", CancellationToken);
        (await UsersTestData.ReadBodyAsync(entitlement, CancellationToken)).GetProperty("tier").GetString().Should().Be("Base");
        var stored = await ReadSubscriptionAsync(subscriptionId);
        (stored.StudentId, stored.PaymobReference, stored.CreatedBy).Should().Be((student.Id, (string?)null, (Guid?)adminUser.Id));
        var audit = await ContentTestData.ReadAuditAsync(factory, "Subscription.GrantComplimentary", subscriptionId, CancellationToken);
        (audit.Outcome, audit.Diff is null).Should().Be(("Success", false));
    }

    [Fact]
    public async Task Grant_AskTeacherWithoutBase_Returns400()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await GrantAsync(admin, student.Id, "AskTeacher", "Monthly");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await UsersTestData.ReadCodeAsync(response, CancellationToken)).Should().Be("COMPLIMENTARY_REQUIRES_BASE");
    }

    [Fact]
    public async Task Grant_HeldBase_Returns400()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        await SubscriptionTestData.SeedSubscriptionAsync(factory, new SubscriptionBuilder().ForStudent(student.Id).StartingAt(DateTimeOffset.UtcNow.AddDays(-1)).Build(), CancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await GrantAsync(admin, student.Id, "Base", "Yearly");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await UsersTestData.ReadCodeAsync(response, CancellationToken)).Should().Be("COMPLIMENTARY_PLAN_ALREADY_ACTIVE");
    }

    [Fact]
    public async Task Grant_AskTeacherTermly_Returns400()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await GrantAsync(admin, student.Id, "AskTeacher", "Termly");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await UsersTestData.ReadCodeAsync(response, CancellationToken)).Should().Be("COMPLIMENTARY_PERIOD_UNAVAILABLE");
    }

    [Fact]
    public async Task Grant_UnknownStudent_Returns404()
    {
        using var admin = await AdminClientAsync();

        using var response = await GrantAsync(admin, Guid.NewGuid(), "Base", "Monthly");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await UsersTestData.ReadCodeAsync(response, CancellationToken)).Should().Be("STUDENT_NOT_FOUND");
    }

    [Fact]
    public async Task Grant_TeacherCaller_Returns403()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, CancellationToken);

        using var response = await GrantAsync(client, student.Id, "Base", "Monthly");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static Task<HttpResponseMessage> GrantAsync(HttpClient client, Guid studentId, string plan, string period) => client.PostAsJsonAsync($"/api/students/{studentId}/complimentary-subscriptions", new { plan, period }, CancellationToken);

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = await ScopeTestData.SeedAdminAsync(factory, CancellationToken).ConfigureAwait(false);
        return await ScopeTestData.SignedInClientAsync(factory, admin, CancellationToken).ConfigureAwait(false);
    }

    private async Task<Subscription> ReadSubscriptionAsync(Guid subscriptionId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Subscriptions.AsNoTracking().SingleAsync(x => x.Id == subscriptionId, CancellationToken).ConfigureAwait(false);
    }
}
