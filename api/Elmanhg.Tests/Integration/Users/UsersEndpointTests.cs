using Elmanhg.Domain.Identity;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using static Elmanhg.Tests.Integration.Users.UsersTestData;

namespace Elmanhg.Tests.Integration.Users;

public sealed class UsersEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_StudentsSearchByExactPhone_ReturnsMaskedStudent()
    {
        var phone = AuthTestClient.NewPhoneNumber();
        var student = await AuthTestClient.SeedUserAsync(factory, User.CreateStudentWithPhone("Student", phone), ScopeTestData.Password, suspended: false, CancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.GetAsync($"{UsersRoute}?role=Student&search={phone}", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(CancellationToken)).Should().NotContain(phone);
        var item = (await ReadBodyAsync(response, CancellationToken)).GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
        item.GetProperty("id").GetGuid().Should().Be(student.Id);
        item.GetProperty("maskedPhone").GetString().Should().Be($"010*****{phone[^3..]}");
        item.GetProperty("tier").GetString().Should().Be("Free");
    }

    [Fact]
    public async Task Get_SearchByNameFragment_ReturnsMatchingUserOnly()
    {
        var unique = Guid.NewGuid().ToString("N");
        var student = await AuthTestClient.SeedUserAsync(factory, User.CreateStudentWithEmail($"Student {unique}", AuthTestClient.NewEmail()), ScopeTestData.Password, suspended: false, CancellationToken);
        await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.GetAsync($"{UsersRoute}?role=Student&search={unique[..12].ToUpperInvariant()}", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = (await ReadBodyAsync(response, CancellationToken)).GetProperty("items").EnumerateArray().ToList();
        items.Select(x => x.GetProperty("id").GetGuid()).Should().Equal(student.Id);
    }

    [Fact]
    public async Task Get_TeachersTab_ReturnsSubjectIdsAndPendingInvitation()
    {
        var email = AuthTestClient.NewEmail();
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, "Physics", CancellationToken);
        using var admin = await AdminClientAsync();
        var teacherId = await InviteAsync(admin, "Teacher", email, CancellationToken);
        await ScopeTestData.AssignAsync(factory, teacherId, subjectId, CancellationToken);

        using var response = await admin.GetAsync($"{UsersRoute}?role=Teacher&search={email}", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = (await ReadBodyAsync(response, CancellationToken)).GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
        item.GetProperty("id").GetGuid().Should().Be(teacherId);
        item.GetProperty("invitationPending").GetBoolean().Should().BeTrue();
        item.GetProperty("subjectIds").EnumerateArray().Select(x => x.GetGuid()).Should().Equal(subjectId);
    }

    [Fact]
    public async Task Get_PageSizeAboveMax_Returns422()
    {
        using var admin = await AdminClientAsync();

        using var response = await admin.GetAsync($"{UsersRoute}?pageSize=101", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response, CancellationToken)).Should().Contain("USER_LIST_PAGE_SIZE_INVALID");
    }

    [Fact]
    public async Task Get_Teacher_Returns403()
    {
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, CancellationToken);

        using var response = await client.GetAsync(UsersRoute, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        using var client = AuthTestClient.Create(factory);

        using var response = await client.GetAsync(UsersRoute, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Invite_Teacher_CreatesPendingTeacherAndAudits()
    {
        var email = AuthTestClient.NewEmail();
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsJsonAsync(InvitationsRoute, new { role = "Teacher", displayName = "New teacher", email }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadBodyAsync(response, CancellationToken);
        var userId = body.GetProperty("userId").GetGuid();
        body.GetProperty("emailSent").GetBoolean().Should().BeFalse();
        var stored = await ReadUserAsync(factory, userId, CancellationToken);
        (stored.Role, stored.Email, stored.PasswordHash, stored.IsActive).Should().Be((UserRole.Teacher, email, (string?)null, true));
        (await ContentTestData.ReadAuditAsync(factory, "User.Invite", userId, CancellationToken)).Outcome.Should().Be("Success");
    }

    [Fact]
    public async Task Invite_RegisteredEmail_Returns409()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsJsonAsync(InvitationsRoute, new { role = "Teacher", displayName = "Teacher", email = student.Email }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadCodeAsync(response, CancellationToken)).Should().Be("EMAIL_ALREADY_REGISTERED");
    }

    [Fact]
    public async Task Invite_StudentRole_Returns422()
    {
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsJsonAsync(InvitationsRoute, new { role = "Student", displayName = "Student", email = AuthTestClient.NewEmail() }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response, CancellationToken)).Should().Contain("USER_INVITE_ROLE_INVALID");
    }

    [Fact]
    public async Task Suspend_SignedInStudent_RevokesAccessAndRefreshTokens()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        using var studentClient = AuthTestClient.Create(factory);
        using var login = await studentClient.PostAsJsonAsync("/api/auth/login/email", new { email = student.Email, password = ScopeTestData.Password }, CancellationToken);
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var cookie = AuthTestClient.ReadRefreshCookie(login);
        studentClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await ReadBodyAsync(login, CancellationToken)).GetProperty("accessToken").GetString());
        using (var warm = await studentClient.GetAsync("/api/progress/subjects", CancellationToken))
        {
            warm.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using var admin = await AdminClientAsync();

        using var suspend = await admin.PostAsync($"{UsersRoute}/{student.Id}/suspend", null, CancellationToken);

        suspend.StatusCode.Should().Be(HttpStatusCode.OK);
        using var afterSuspend = await studentClient.GetAsync("/api/progress/subjects", CancellationToken);
        afterSuspend.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        refreshRequest.Headers.Add("Cookie", cookie);
        using var anonymous = AuthTestClient.Create(factory);
        using var refresh = await anonymous.SendAsync(refreshRequest, CancellationToken);
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ReadUserAsync(factory, student.Id, CancellationToken)).Status.Should().Be(UserStatus.Suspended);
        (await ContentTestData.ReadAuditAsync(factory, "User.Suspend", student.Id, CancellationToken)).Outcome.Should().Be("Success");
    }

    [Fact]
    public async Task Suspend_Self_Returns400()
    {
        var admin = await ScopeTestData.SeedAdminAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, admin, CancellationToken);

        using var response = await client.PostAsync($"{UsersRoute}/{admin.Id}/suspend", null, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response, CancellationToken)).Should().Be("USER_CANNOT_SUSPEND_SELF");
        (await ReadUserAsync(factory, admin.Id, CancellationToken)).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Suspend_UnknownUser_Returns404()
    {
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsync($"{UsersRoute}/{Guid.NewGuid()}/suspend", null, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response, CancellationToken)).Should().Be("USER_NOT_FOUND");
    }

    [Fact]
    public async Task Suspend_TeacherCaller_Returns403()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, CancellationToken);

        using var response = await client.PostAsync($"{UsersRoute}/{student.Id}/suspend", null, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadUserAsync(factory, student.Id, CancellationToken)).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Reactivate_SuspendedStudent_AllowsSignInAgain()
    {
        var student = await AuthTestClient.SeedUserAsync(factory, User.CreateStudentWithEmail("Student", AuthTestClient.NewEmail()), ScopeTestData.Password, suspended: true, CancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsync($"{UsersRoute}/{student.Id}/reactivate", null, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var anonymous = AuthTestClient.Create(factory);
        using var login = await anonymous.PostAsJsonAsync("/api/auth/login/email", new { email = student.Email, password = ScopeTestData.Password }, CancellationToken);
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ContentTestData.ReadAuditAsync(factory, "User.Reactivate", student.Id, CancellationToken)).Outcome.Should().Be("Success");
    }

    [Fact]
    public async Task Reactivate_ActiveUser_Returns400()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        using var admin = await AdminClientAsync();

        using var response = await admin.PostAsync($"{UsersRoute}/{student.Id}/reactivate", null, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response, CancellationToken)).Should().Be("USER_NOT_SUSPENDED");
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = await ScopeTestData.SeedAdminAsync(factory, CancellationToken).ConfigureAwait(false);
        return await ScopeTestData.SignedInClientAsync(factory, admin, CancellationToken).ConfigureAwait(false);
    }
}
