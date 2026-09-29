using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.TeacherThreads.TeacherThreadTestData;

namespace Elmanhg.Tests.Integration.TeacherThreads;

public sealed class TeacherThreadMediaEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_OwningStudent_ServesPrivateImage()
    {
        var (_, imageUrl, owner) = await SeedThreadWithImageAsync();

        using var response = await owner.GetAsync(imageUrl, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync(CancellationToken)).Should().Equal(PngBytes);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle().Which.Should().Be("nosniff");
        response.Headers.CacheControl!.Private.Should().BeTrue();
    }

    [Fact]
    public async Task Get_Anonymous_Returns404()
    {
        var (_, imageUrl, _) = await SeedThreadWithImageAsync();
        using var anonymous = AuthTestClient.Create(factory);

        using var response = await anonymous.GetAsync(imageUrl, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_OtherStudent_Returns404()
    {
        var (_, imageUrl, _) = await SeedThreadWithImageAsync();
        var (_, other) = await SignedInAskTeacherStudentAsync(factory);

        using var response = await other.GetAsync(imageUrl, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_TeacherScopedToTheSubject_ServesImage()
    {
        var (subjectId, imageUrl, _) = await SeedThreadWithImageAsync();
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        await ScopeTestData.AssignAsync(factory, teacher.Id, subjectId, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, CancellationToken);

        using var response = await client.GetAsync(imageUrl, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync(CancellationToken)).Should().Equal(PngBytes);
    }

    [Fact]
    public async Task Get_TeacherOutsideTheSubject_Returns404()
    {
        var (_, imageUrl, _) = await SeedThreadWithImageAsync();
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, CancellationToken);

        using var response = await client.GetAsync(imageUrl, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_Admin_ServesImage()
    {
        var (_, imageUrl, _) = await SeedThreadWithImageAsync();
        var admin = await ScopeTestData.SeedAdminAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, admin, CancellationToken);

        using var response = await client.GetAsync(imageUrl, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/api/media//teacher-threads/{0}")]
    [InlineData("/api/media/Teacher-Threads/{0}")]
    [InlineData("/api/media/teacher-threads./{0}")]
    [InlineData("/api/media/teacher-threads%5C{0}")]
    [InlineData("/api/media/lessons/..%5Cteacher-threads%5C{0}")]
    public async Task Get_NonCanonicalPathAnonymously_Returns404(string pathFormat)
    {
        var (_, imageUrl, _) = await SeedThreadWithImageAsync();
        using var anonymous = AuthTestClient.Create(factory);

        using var response = await anonymous.GetAsync(string.Format(pathFormat, Path.GetFileName(imageUrl)), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<(Guid SubjectId, string ImageUrl, HttpClient Owner)> SeedThreadWithImageAsync()
    {
        var (subjectId, lessonId) = await SeedPublishedLessonAsync(factory);
        var (_, owner) = await SignedInAskTeacherStudentAsync(factory);
        using var response = await owner.PostAsync(Route, QuestionForm("Why is F = ma?", lessonId: lessonId, imageFileName: "photo.png"), CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        return (subjectId, body.GetProperty("messages")[0].GetProperty("imageUrl").GetString()!, owner);
    }
}
