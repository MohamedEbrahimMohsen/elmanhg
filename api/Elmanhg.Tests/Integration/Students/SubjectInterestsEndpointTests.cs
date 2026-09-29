using Elmanhg.Domain.Identity;
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

namespace Elmanhg.Tests.Integration.Students;

public sealed class SubjectInterestsEndpointTests(ApiFactory factory)
{
    private const string Route = "/api/students/me/subject-interests";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        using var anonymous = AuthTestClient.Create(factory);

        using var response = await anonymous.GetAsync(Route, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Teacher_Returns403()
    {
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, CancellationToken);

        using var response = await client.GetAsync(Route, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_NewStudent_ReturnsNeedsOnboardingAndSeededSubjectUnselected()
    {
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, "Physics", CancellationToken);
        var (_, client) = await SignedInStudentAsync();

        var interests = await GetInterestsAsync(client);

        interests.GetProperty("needsOnboarding").GetBoolean().Should().BeTrue();
        Subject(interests, subjectId).GetProperty("isSelected").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Put_ChosenSubject_PersistsAndClearsNeedsOnboarding()
    {
        var first = await ScopeTestData.SeedSubjectAsync(factory, "Physics", CancellationToken);
        var second = await ScopeTestData.SeedSubjectAsync(factory, "Chemistry", CancellationToken);
        var (student, client) = await SignedInStudentAsync();

        using var response = await client.PutAsJsonAsync(Route, new { subjectIds = new[] { first } }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var interests = await GetInterestsAsync(client);
        interests.GetProperty("needsOnboarding").GetBoolean().Should().BeFalse();
        Subject(interests, first).GetProperty("isSelected").GetBoolean().Should().BeTrue();
        Subject(interests, second).GetProperty("isSelected").GetBoolean().Should().BeFalse();
        var stored = await ReadUserAsync(student.Id);
        stored.OnboardedAt.Should().NotBeNull();
        stored.SubjectInterestIds.Should().Equal(first);
    }

    [Fact]
    public async Task Put_EmptyList_CompletesOnboarding()
    {
        var (student, client) = await SignedInStudentAsync();

        using var response = await client.PutAsJsonAsync(Route, new { subjectIds = Array.Empty<Guid>() }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await ReadUserAsync(student.Id);
        stored.OnboardedAt.Should().NotBeNull();
        stored.SubjectInterestIds.Should().BeEmpty();
    }

    [Fact]
    public async Task Put_UnknownSubject_Returns404()
    {
        var (student, client) = await SignedInStudentAsync();

        using var response = await client.PutAsJsonAsync(Route, new { subjectIds = new[] { Guid.NewGuid() } }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadBodyAsync(response)).GetProperty("code").GetString().Should().Be("SUBJECT_NOT_FOUND");
        (await ReadUserAsync(student.Id)).OnboardedAt.Should().BeNull();
    }

    [Fact]
    public async Task Put_DuplicateIds_Returns422()
    {
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, "Physics", CancellationToken);
        var (_, client) = await SignedInStudentAsync();

        using var response = await client.PutAsJsonAsync(Route, new { subjectIds = new[] { subjectId, subjectId } }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadBodyAsync(response)).GetProperty("code").GetString().Should().Contain("SUBJECT_INTERESTS_DUPLICATE");
    }

    [Fact]
    public async Task Put_Admin_Returns403()
    {
        var admin = await ScopeTestData.SeedAdminAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, admin, CancellationToken);

        using var response = await client.PutAsJsonAsync(Route, new { subjectIds = Array.Empty<Guid>() }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Put_Anonymous_Returns401()
    {
        using var anonymous = AuthTestClient.Create(factory);

        using var response = await anonymous.PutAsJsonAsync(Route, new { subjectIds = Array.Empty<Guid>() }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<(User Student, HttpClient Client)> SignedInStudentAsync()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken).ConfigureAwait(false);
        return (student, await ScopeTestData.SignedInClientAsync(factory, student, CancellationToken).ConfigureAwait(false));
    }

    private static async Task<JsonElement> GetInterestsAsync(HttpClient client)
    {
        using var response = await client.GetAsync(Route, CancellationToken).ConfigureAwait(false);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await ReadBodyAsync(response).ConfigureAwait(false);
    }

    private static JsonElement Subject(JsonElement interests, Guid subjectId) => interests.GetProperty("subjects").EnumerateArray().Single(x => x.GetProperty("subjectId").GetGuid() == subjectId);

    private static async Task<JsonElement> ReadBodyAsync(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken).ConfigureAwait(false);

    private async Task<User> ReadUserAsync(Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.Users.AsNoTracking().SingleAsync(x => x.Id == userId, CancellationToken).ConfigureAwait(false);
    }
}
