using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Teachers;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.Authorization;

public static class ScopeTestData
{
    public const string Password = "Passw0rd1";

    public static Task<User> SeedTeacherAsync(ApiFactory factory, CancellationToken cancellationToken) => AuthTestClient.SeedUserAsync(factory, User.CreateTeacher("Teacher", AuthTestClient.NewEmail()), Password, suspended: false, cancellationToken);

    public static Task<User> SeedAdminAsync(ApiFactory factory, CancellationToken cancellationToken) => AuthTestClient.SeedUserAsync(factory, User.CreateAdmin("Admin", AuthTestClient.NewEmail()), Password, suspended: false, cancellationToken);

    public static Task<User> SeedStudentAsync(ApiFactory factory, CancellationToken cancellationToken) => AuthTestClient.SeedUserAsync(factory, User.CreateStudentWithEmail("Student", AuthTestClient.NewEmail()), Password, suspended: false, cancellationToken);

    public static async Task<Guid> SeedSubjectAsync(ApiFactory factory, string name, CancellationToken cancellationToken)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var subject = Subject.Create(name, Guid.NewGuid());
        context.Subjects.Add(subject);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return subject.Id;
    }

    public static async Task AssignAsync(ApiFactory factory, Guid teacherId, Guid subjectId, CancellationToken cancellationToken)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var teacher = await context.Users.SingleAsync(x => x.Id == teacherId, cancellationToken).ConfigureAwait(false);
        var subject = await context.Subjects.SingleAsync(x => x.Id == subjectId, cancellationToken).ConfigureAwait(false);
        context.TeacherSubjects.Add(TeacherSubject.Create(teacher, subject, Guid.NewGuid()));
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<HttpClient> SignedInClientAsync(ApiFactory factory, User user, CancellationToken cancellationToken)
    {
        var client = AuthTestClient.Create(factory);
        using var response = await client.PostAsJsonAsync("/api/auth/login/email", new { email = user.Email, password = Password }, cancellationToken).ConfigureAwait(false);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }
}
