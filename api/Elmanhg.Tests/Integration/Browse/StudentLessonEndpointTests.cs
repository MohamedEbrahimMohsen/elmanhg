using Elmanhg.Domain.Lessons;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Browse;

public sealed class StudentLessonEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_Teacher_Returns403()
    {
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, CancellationToken);

        using var response = await client.GetAsync($"{BrowseTestData.Route}/lessons/{Guid.NewGuid()}", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_DraftLesson_Returns404()
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, CancellationToken);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, CancellationToken);
        var draft = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Draft", 1, LessonState.Draft, CancellationToken);
        var (_, client) = await SignedInStudentAsync(factory);

        using var response = await client.GetAsync($"{BrowseTestData.Route}/lessons/{draft}", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString().Should().Be("LESSON_NOT_FOUND");
    }

    [Fact]
    public async Task Get_PublishedLesson_ReturnsContentBreadcrumbAndNeighbours()
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, CancellationToken);
        var mechanics = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, CancellationToken);
        var waves = await ContentTestData.SeedUnitAsync(factory, subjectId, "Waves", 2, CancellationToken);
        var energy = await ContentTestData.SeedLessonAsync(factory, mechanics, "Energy", 1, ["Define energy", "Apply conservation"], CancellationToken);
        await PublishAsync(energy);
        var waveBasics = await ContentTestData.SeedLessonInStateAsync(factory, waves, "Wave basics", 1, LessonState.Published, CancellationToken);
        var (_, client) = await SignedInStudentAsync(factory);

        var body = await BrowseTestData.GetAsync(client, $"lessons/{energy}");

        body.GetProperty("objectives").EnumerateArray().Select(x => x.GetProperty("text").GetString()).Should().Equal("Define energy", "Apply conservation");
        (body.GetProperty("unitName").GetString(), body.GetProperty("subjectName").GetString()).Should().Be(("Mechanics", "Physics"));
        body.GetProperty("previousLesson").ValueKind.Should().Be(JsonValueKind.Null);
        var next = body.GetProperty("nextLesson");
        (next.GetProperty("id").GetGuid(), next.GetProperty("unitId").GetGuid(), next.GetProperty("unitName").GetString()).Should().Be((waveBasics, waves, "Waves"));
    }

    private async Task PublishAsync(Guid lessonId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lesson = await context.Lessons.SingleAsync(x => x.Id == lessonId, CancellationToken).ConfigureAwait(false);
        lesson.Publish(Guid.NewGuid());
        lesson.ClearDomainEvents();
        await context.SaveChangesAsync(CancellationToken).ConfigureAwait(false);
    }
}
