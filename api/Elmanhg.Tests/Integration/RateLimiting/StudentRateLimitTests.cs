using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.TeacherThreads;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.RateLimiting;

public sealed class StudentRateLimitTests(ApiFactory factory)
{
    private const string AvatarRoute = "/api/avatar/messages";

    [Fact]
    public async Task SendAvatarMessage_OverStudentLimit_Returns429TooManyRequests()
    {
        await using var host = LimitedHost("RateLimiting:AvatarMessagePermitLimit");
        using var client = await StudentClientAsync(host);
        using var first = await client.PostAsJsonAsync(AvatarRoute, AvatarMessage(), TestContext.Current.CancellationToken);

        using var second = await client.PostAsJsonAsync(AvatarRoute, AvatarMessage(), TestContext.Current.CancellationToken);

        first.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        second.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await ReadCodeAsync(second)).Should().Be("TOO_MANY_REQUESTS");
    }

    [Fact]
    public async Task SendAvatarMessage_TwoStudentsAtLimitOne_NeitherIsLimited()
    {
        await using var host = LimitedHost("RateLimiting:AvatarMessagePermitLimit");
        using var firstStudent = await StudentClientAsync(host);
        using var secondStudent = await StudentClientAsync(host);

        using var first = await firstStudent.PostAsJsonAsync(AvatarRoute, AvatarMessage(), TestContext.Current.CancellationToken);
        using var second = await secondStudent.PostAsJsonAsync(AvatarRoute, AvatarMessage(), TestContext.Current.CancellationToken);

        first.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        second.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task FollowUpTeacherThread_OverStudentLimit_Returns429TooManyRequests()
    {
        await using var host = LimitedHost("RateLimiting:AskTeacherSubmissionPermitLimit");
        using var client = await AskTeacherClientAsync(host);
        using var first = await client.PostAsJsonAsync($"/api/teacher-threads/{Guid.NewGuid()}/follow-ups", new { text = "x" }, TestContext.Current.CancellationToken);

        using var second = await client.PostAsJsonAsync($"/api/teacher-threads/{Guid.NewGuid()}/follow-ups", new { text = "x" }, TestContext.Current.CancellationToken);

        first.StatusCode.Should().Be(HttpStatusCode.NotFound);
        second.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await ReadCodeAsync(second)).Should().Be("TOO_MANY_REQUESTS");
    }

    [Fact]
    public async Task CreateTeacherThread_OverStudentLimit_Returns429TooManyRequests()
    {
        await using var host = LimitedHost("RateLimiting:AskTeacherSubmissionPermitLimit");
        using var client = await AskTeacherClientAsync(host);
        using var firstForm = TeacherThreadTestData.QuestionForm("سؤال");
        using var first = await client.PostAsync("/api/teacher-threads", firstForm, TestContext.Current.CancellationToken);
        using var secondForm = TeacherThreadTestData.QuestionForm("سؤال");

        using var second = await client.PostAsync("/api/teacher-threads", secondForm, TestContext.Current.CancellationToken);

        first.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        second.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await ReadCodeAsync(second)).Should().Be("TOO_MANY_REQUESTS");
    }

    private WebApplicationFactory<Program> LimitedHost(string permitLimitKey) => factory.WithWebHostBuilder(builder => builder.UseSetting(permitLimitKey, "1"));

    private static object AvatarMessage() => new { entryPoint = "Lesson", lessonId = Guid.NewGuid(), message = "x" };

    private async Task<HttpClient> StudentClientAsync(WebApplicationFactory<Program> host)
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, TestContext.Current.CancellationToken).ConfigureAwait(false);
        using var signedIn = await ScopeTestData.SignedInClientAsync(factory, student, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return OnHost(host, signedIn);
    }

    private async Task<HttpClient> AskTeacherClientAsync(WebApplicationFactory<Program> host)
    {
        var (_, signedIn) = await TeacherThreadTestData.SignedInAskTeacherStudentAsync(factory).ConfigureAwait(false);
        using (signedIn)
        {
            return OnHost(host, signedIn);
        }
    }

    private static HttpClient OnHost(WebApplicationFactory<Program> host, HttpClient signedIn)
    {
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Authorization = signedIn.DefaultRequestHeaders.Authorization;
        return client;
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return body.GetProperty("code").GetString();
    }
}
