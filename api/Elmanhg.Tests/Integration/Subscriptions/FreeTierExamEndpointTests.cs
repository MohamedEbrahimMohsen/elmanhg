using Elmanhg.Domain.Sessions;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Exams;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Subscriptions;

public sealed class FreeTierExamEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PostUnitExam_FreeStudent_Returns403AndStoresNoSession()
    {
        var (_, unitId, _, _) = await ExamTestData.SeedExamUnitAsync(factory, 2);
        var (student, client) = await SignedInFreeStudentAsync(factory);

        using var response = await client.PostAsync($"{ExamTestData.Route}/units/{unitId}", null, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ExamTestData.ReadCodeAsync(response)).Should().Be("EXAM_REQUIRES_SUBSCRIPTION");
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await context.Sessions.AnyAsync(x => x.StudentId == student.Id && x.Kind != SessionKind.Quiz, CancellationToken)).Should().BeFalse();
    }

    [Fact]
    public async Task PostMultiUnitExam_FreeStudent_Returns403()
    {
        var (subjectId, unitIds, _) = await MultiUnitExamTestData.SeedMultiUnitSubjectAsync(factory, [10, 10]);
        var (_, client) = await SignedInFreeStudentAsync(factory);

        using var response = await MultiUnitExamTestData.StartMultiAsync(client, subjectId, unitIds, 20);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ExamTestData.ReadCodeAsync(response)).Should().Be("EXAM_REQUIRES_SUBSCRIPTION");
    }

    [Fact]
    public async Task PostUnitExam_AdminWithoutSubscription_Returns200TestMode()
    {
        var (_, unitId, _, _) = await ExamTestData.SeedExamUnitAsync(factory, 2);
        var admin = await ScopeTestData.SeedAdminAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, admin, CancellationToken);

        var body = await ExamTestData.StartAsync(client, unitId);

        body.GetProperty("isTestMode").GetBoolean().Should().BeTrue();
    }
}
