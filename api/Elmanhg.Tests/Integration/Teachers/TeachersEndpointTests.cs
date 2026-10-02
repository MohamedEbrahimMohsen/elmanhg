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

namespace Elmanhg.Tests.Integration.Teachers;

public sealed class TeachersEndpointTests(ApiFactory factory)
{
    private const string Route = "/api/teachers";

    [Fact]
    public async Task Get_Admin_ReturnsTeachersOnly()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, cancellationToken);
        var student = await ScopeTestData.SeedStudentAsync(factory, cancellationToken);
        var admin = await ScopeTestData.SeedAdminAsync(factory, cancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, admin, cancellationToken);

        using var response = await client.GetAsync(Route, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var teachers = (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).EnumerateArray().ToList();
        var ids = teachers.Select(x => x.GetProperty("id").GetGuid()).ToList();
        teachers.Should().Contain(x => x.GetProperty("id").GetGuid() == teacher.Id && x.GetProperty("displayName").GetString() == "Teacher");
        ids.Should().NotContain(admin.Id).And.NotContain(student.Id);
    }

    [Fact]
    public async Task Get_Teacher_Returns403()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, cancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, cancellationToken);

        using var response = await client.GetAsync(Route, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        using var client = AuthTestClient.Create(factory);

        using var response = await client.GetAsync(Route, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_PhoneNumber_AsAdmin_SetsNumberAndWritesAuditRowWithoutIt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, cancellationToken);
        var phone = AuthTestClient.NewPhoneNumber();
        using var client = await AdminClientAsync(cancellationToken);

        using var response = await client.PutAsJsonAsync(PhoneRoute(teacher.Id), new { phoneNumber = phone }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadUserAsync(teacher.Id, cancellationToken)).PhoneNumber.Should().Be(phone);
        using var scope = factory.Services.CreateScope();
        var audit = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs.AsNoTracking().SingleAsync(x => x.Action == "Teacher.SetPhoneNumber" && x.ResourceId == teacher.Id, cancellationToken);
        (audit.ResourceType, audit.Outcome).Should().Be(("Teacher", "Success"));
        JsonSerializer.Serialize(audit).Should().NotContain(phone);
    }

    [Fact]
    public async Task Put_NullPhoneNumber_ClearsNumber()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, cancellationToken);
        using var client = await AdminClientAsync(cancellationToken);
        (await client.PutAsJsonAsync(PhoneRoute(teacher.Id), new { phoneNumber = AuthTestClient.NewPhoneNumber() }, cancellationToken)).Dispose();

        using var response = await client.PutAsJsonAsync(PhoneRoute(teacher.Id), new { phoneNumber = (string?)null }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadUserAsync(teacher.Id, cancellationToken)).PhoneNumber.Should().BeNull();
    }

    [Fact]
    public async Task Put_InvalidPhoneNumber_Returns422()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, cancellationToken);
        using var client = await AdminClientAsync(cancellationToken);

        using var response = await client.PutAsJsonAsync(PhoneRoute(teacher.Id), new { phoneNumber = "02012345678" }, cancellationToken);

        await ExpectProblemAsync(response, HttpStatusCode.UnprocessableEntity, "VALIDATION_PHONE_NUMBER_INVALID_CELLULAR_CODE");
        (await ReadUserAsync(teacher.Id, cancellationToken)).PhoneNumber.Should().BeNull();
    }

    [Fact]
    public async Task Put_StudentId_Returns400PhoneNumberTeachersOnly()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var student = await ScopeTestData.SeedStudentAsync(factory, cancellationToken);
        using var client = await AdminClientAsync(cancellationToken);

        using var response = await client.PutAsJsonAsync(PhoneRoute(student.Id), new { phoneNumber = "01012345678" }, cancellationToken);

        await ExpectProblemAsync(response, HttpStatusCode.BadRequest, "PHONE_NUMBER_TEACHERS_ONLY");
    }

    [Fact]
    public async Task Put_UnknownId_Returns404()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = await AdminClientAsync(cancellationToken);

        using var response = await client.PutAsJsonAsync(PhoneRoute(Guid.NewGuid()), new { phoneNumber = "01012345678" }, cancellationToken);

        await ExpectProblemAsync(response, HttpStatusCode.NotFound, "USER_NOT_FOUND");
    }

    [Fact]
    public async Task Put_AsTeacher_Returns403()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, cancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, cancellationToken);

        using var response = await client.PutAsJsonAsync(PhoneRoute(teacher.Id), new { phoneNumber = "01012345678" }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadUserAsync(teacher.Id, cancellationToken)).PhoneNumber.Should().BeNull();
    }

    [Fact]
    public async Task Put_Anonymous_Returns401()
    {
        using var client = AuthTestClient.Create(factory);

        using var response = await client.PutAsJsonAsync(PhoneRoute(Guid.NewGuid()), new { phoneNumber = "01012345678" }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_PhoneNumber_DoesNotEnablePhoneSignIn()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, cancellationToken);
        var phone = AuthTestClient.NewPhoneNumber();
        using (var admin = await AdminClientAsync(cancellationToken))
        {
            (await admin.PutAsJsonAsync(PhoneRoute(teacher.Id), new { phoneNumber = phone }, cancellationToken)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using var anonymous = AuthTestClient.Create(factory);
        var verificationId = await AuthTestClient.SendAndVerifyOtpAsync(anonymous, factory, phone, cancellationToken);

        using var response = await anonymous.PostAsJsonAsync("/api/auth/login/phone", new { verificationId }, cancellationToken);

        await ExpectProblemAsync(response, HttpStatusCode.NotFound, "PHONE_NUMBER_NOT_REGISTERED");
    }

    private static string PhoneRoute(Guid teacherId) => $"{Route}/{teacherId}/phone-number";

    private async Task<HttpClient> AdminClientAsync(CancellationToken cancellationToken)
    {
        var admin = await ScopeTestData.SeedAdminAsync(factory, cancellationToken);
        return await ScopeTestData.SignedInClientAsync(factory, admin, cancellationToken);
    }

    private async Task<User> ReadUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AsNoTracking().SingleAsync(x => x.Id == userId, cancellationToken);
    }

    private static async Task ExpectProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.Should().Be(status);
        (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("code").GetString().Should().Be(code);
    }
}
