using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.Content;

public sealed class LessonImagesEndpointTests(ApiFactory factory)
{
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47];

    [Fact]
    public async Task Post_Admin_StoresServesAndAuditsImage()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, lessonId) = await SeedLessonAsync();
        using var admin = await AdminClientAsync();
        using var form = ImageForm("diagram.png", "image/png");

        using var response = await admin.PostAsync(Route(lessonId), form, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var url = (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("url").GetString();
        url.Should().StartWith($"/api/media/lessons/{lessonId}/").And.EndWith(".png");
        using var served = await admin.GetAsync(url, cancellationToken);
        served.StatusCode.Should().Be(HttpStatusCode.OK);
        (await served.Content.ReadAsByteArrayAsync(cancellationToken)).Should().Equal(PngBytes);
        served.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle().Which.Should().Be("nosniff");
        (await ContentTestData.ReadAuditAsync(factory, "Lesson.UploadImage", lessonId, cancellationToken)).Outcome.Should().Be("Success");
    }

    [Fact]
    public async Task Post_SvgFile_Returns422LessonImageTypeInvalid()
    {
        var (_, lessonId) = await SeedLessonAsync();
        using var admin = await AdminClientAsync();
        using var form = ImageForm("diagram.svg", "image/svg+xml");

        using var response = await admin.PostAsync(Route(lessonId), form, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("LESSON_IMAGE_TYPE_INVALID");
    }

    [Fact]
    public async Task Post_UnknownLesson_Returns404LessonNotFound()
    {
        using var admin = await AdminClientAsync();
        using var form = ImageForm("diagram.png", "image/png");

        using var response = await admin.PostAsync(Route(Guid.NewGuid()), form, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("LESSON_NOT_FOUND");
    }

    [Fact]
    public async Task Post_Teacher_Returns403()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (subjectId, lessonId) = await SeedLessonAsync();
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, cancellationToken);
        await ScopeTestData.AssignAsync(factory, teacher.Id, subjectId, cancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, cancellationToken);
        using var form = ImageForm("diagram.png", "image/png");

        using var response = await client.PostAsync(Route(lessonId), form, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_Anonymous_Returns401()
    {
        using var client = AuthTestClient.Create(factory);
        using var form = ImageForm("diagram.png", "image/png");

        using var response = await client.PostAsync(Route(Guid.NewGuid()), form, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static string Route(Guid lessonId) => $"/api/lessons/{lessonId}/images";

    private static MultipartFormDataContent ImageForm(string fileName, string contentType)
    {
        var file = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47]);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    private async Task<(Guid SubjectId, Guid LessonId)> SeedLessonAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, cancellationToken).ConfigureAwait(false);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, cancellationToken).ConfigureAwait(false);
        var lessonId = await ContentTestData.SeedLessonAsync(factory, unitId, "Newton's laws", 1, [], cancellationToken).ConfigureAwait(false);
        return (subjectId, lessonId);
    }

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = await ScopeTestData.SeedAdminAsync(factory, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return await ScopeTestData.SignedInClientAsync(factory, admin, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return body.GetProperty("code").GetString();
    }
}
