using Elmanhg.Domain.Units;
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

namespace Elmanhg.Tests.Integration.Content;

public sealed class UnitsEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Post_Admin_AppendsUnitAtEndAndAudits()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var subjectId = await SeedSubjectAsync();
        await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, cancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsJsonAsync(Route(subjectId), new { name = "Waves" }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();
        (await ContentTestData.ReadOrderAsync(factory, id, cancellationToken)).Should().Be(2);
        (await ContentTestData.ReadAuditAsync(factory, "Unit.Create", id, cancellationToken)).Outcome.Should().Be("Success");
    }

    [Fact]
    public async Task Post_UnknownSubject_Returns404SubjectNotFound()
    {
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsJsonAsync(Route(Guid.NewGuid()), new { name = "Waves" }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("SUBJECT_NOT_FOUND");
    }

    [Fact]
    public async Task Post_EmptyName_Returns422UnitNameRequired()
    {
        var subjectId = await SeedSubjectAsync();
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsJsonAsync(Route(subjectId), new { name = string.Empty }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("UNIT_NAME_REQUIRED");
    }

    [Fact]
    public async Task Post_Teacher_Returns403()
    {
        var subjectId = await SeedSubjectAsync();
        using var client = await TeacherClientAsync(subjectId);

        using var response = await client.PostAsJsonAsync(Route(subjectId), new { name = "Waves" }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_Anonymous_Returns401()
    {
        using var client = AuthTestClient.Create(factory);

        using var response = await client.PostAsJsonAsync(Route(Guid.NewGuid()), new { name = "Waves" }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_Admin_RenamesUnit()
    {
        var subjectId = await SeedSubjectAsync();
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, TestContext.Current.CancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.PutAsJsonAsync($"{Route(subjectId)}/{unitId}", new { name = "Waves" }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadUnitAsync(unitId)).Name.Should().Be("Waves");
    }

    [Fact]
    public async Task Put_UnitOfOtherSubject_Returns404UnitNotFound()
    {
        var subjectId = await SeedSubjectAsync();
        var otherSubjectId = await SeedSubjectAsync();
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, TestContext.Current.CancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.PutAsJsonAsync($"{Route(otherSubjectId)}/{unitId}", new { name = "Waves" }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("UNIT_NOT_FOUND");
        (await ReadUnitAsync(unitId)).Name.Should().Be("Mechanics");
    }

    [Fact]
    public async Task Put_Teacher_Returns403()
    {
        var subjectId = await SeedSubjectAsync();
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, TestContext.Current.CancellationToken);
        using var client = await TeacherClientAsync(subjectId);

        using var response = await client.PutAsJsonAsync($"{Route(subjectId)}/{unitId}", new { name = "Waves" }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PutPosition_MoveLastToFirst_RenumbersUnits()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var subjectId = await SeedSubjectAsync();
        var first = await ContentTestData.SeedUnitAsync(factory, subjectId, "A", 1, cancellationToken);
        var second = await ContentTestData.SeedUnitAsync(factory, subjectId, "B", 2, cancellationToken);
        var third = await ContentTestData.SeedUnitAsync(factory, subjectId, "C", 3, cancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.PutAsJsonAsync($"{Route(subjectId)}/{third}/position", new { position = 1 }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ContentTestData.ReadOrderAsync(factory, third, cancellationToken)).Should().Be(1);
        (await ContentTestData.ReadOrderAsync(factory, first, cancellationToken)).Should().Be(2);
        (await ContentTestData.ReadOrderAsync(factory, second, cancellationToken)).Should().Be(3);
    }

    [Fact]
    public async Task PutPosition_PositionZero_Returns422UnitPositionInvalid()
    {
        var subjectId = await SeedSubjectAsync();
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, TestContext.Current.CancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.PutAsJsonAsync($"{Route(subjectId)}/{unitId}/position", new { position = 0 }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("UNIT_POSITION_INVALID");
    }

    [Fact]
    public async Task PutPosition_Teacher_Returns403()
    {
        var subjectId = await SeedSubjectAsync();
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, TestContext.Current.CancellationToken);
        using var client = await TeacherClientAsync(subjectId);

        using var response = await client.PutAsJsonAsync($"{Route(subjectId)}/{unitId}/position", new { position = 1 }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_Admin_SoftDeletesUnit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var subjectId = await SeedSubjectAsync();
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, cancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.DeleteAsync($"{Route(subjectId)}/{unitId}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadUnitAsync(unitId)).IsDeleted.Should().BeTrue();
        using var detail = await admin.GetAsync($"/api/subjects/{subjectId}", cancellationToken);
        var body = await detail.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        body.GetProperty("units").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Delete_UnitWithLessons_Returns400UnitHasLessonsAndAuditsFailure()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var subjectId = await SeedSubjectAsync();
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, cancellationToken);
        await ContentTestData.SeedLessonAsync(factory, unitId, "Newton's laws", 1, [], cancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.DeleteAsync($"{Route(subjectId)}/{unitId}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be("UNIT_HAS_LESSONS");
        (await ReadUnitAsync(unitId)).IsDeleted.Should().BeFalse();
        var audit = await ContentTestData.ReadAuditAsync(factory, "Unit.Delete", unitId, cancellationToken);
        audit.Outcome.Should().Be("Failure");
        audit.ErrorCode.Should().Be("UNIT_HAS_LESSONS");
    }

    [Fact]
    public async Task Delete_UnknownUnit_Returns404UnitNotFound()
    {
        var subjectId = await SeedSubjectAsync();
        using var admin = await AdminClientAsync();

        using var response = await admin.DeleteAsync($"{Route(subjectId)}/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("UNIT_NOT_FOUND");
    }

    [Fact]
    public async Task Delete_Teacher_Returns403()
    {
        var subjectId = await SeedSubjectAsync();
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, TestContext.Current.CancellationToken);
        using var client = await TeacherClientAsync(subjectId);

        using var response = await client.DeleteAsync($"{Route(subjectId)}/{unitId}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadUnitAsync(unitId)).IsDeleted.Should().BeFalse();
    }

    private static string Route(Guid subjectId) => $"/api/subjects/{subjectId}/units";

    private Task<Guid> SeedSubjectAsync() => ContentTestData.SeedSubjectAsync(factory, "Physics", 1, TestContext.Current.CancellationToken);

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = await ScopeTestData.SeedAdminAsync(factory, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return await ScopeTestData.SignedInClientAsync(factory, admin, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpClient> TeacherClientAsync(Guid assignedSubjectId)
    {
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, TestContext.Current.CancellationToken).ConfigureAwait(false);
        await ScopeTestData.AssignAsync(factory, teacher.Id, assignedSubjectId, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return await ScopeTestData.SignedInClientAsync(factory, teacher, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private async Task<CurriculumUnit> ReadUnitAsync(Guid unitId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Units.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == unitId, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return body.GetProperty("code").GetString();
    }
}
