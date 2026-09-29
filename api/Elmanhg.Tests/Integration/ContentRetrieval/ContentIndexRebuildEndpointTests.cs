using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using static Elmanhg.Tests.Integration.ContentRetrieval.ContentRetrievalTestData;

namespace Elmanhg.Tests.Integration.ContentRetrieval;

[Collection(ContentRetrievalCollection.Name)]
public sealed class ContentIndexRebuildEndpointTests(ApiFactory factory)
{
    private const string Route = "/api/content-index/rebuild";
    private const string Explanation = "<p>شدة التيار تتناسب طرديا مع فرق الجهد</p>";

    [Fact]
    public async Task PostRebuild_Admin_ClearsIndexStateAndAudits()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var lessonId = await SeedIndexedLessonAsync();
        var admin = await ScopeTestData.SeedAdminAsync(factory, cancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, admin, cancellationToken);

        using var response = await client.PostAsync(Route, null, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadIndexAsync(factory, lessonId, cancellationToken)).Should().BeNull();
        (await ReadStaleIdsAsync(factory, [], cancellationToken)).Should().Contain(lessonId);
        using var scope = factory.Services.CreateScope();
        var audited = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs
            .AsNoTracking()
            .AnyAsync(x => x.Action == "ContentIndex.Rebuild" && x.ActorUserId == admin.Id && x.Outcome == "Success", cancellationToken);
        audited.Should().BeTrue();
    }

    [Fact]
    public async Task PostRebuild_Teacher_Returns403()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var lessonId = await SeedIndexedLessonAsync();
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, cancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, cancellationToken);

        using var response = await client.PostAsync(Route, null, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadIndexAsync(factory, lessonId, cancellationToken)).Should().NotBeNull();
    }

    private async Task<Guid> SeedIndexedLessonAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var lessonId = await SeedPublishedLessonAsync(factory, Explanation, string.Empty, [], cancellationToken).ConfigureAwait(false);
        await ReindexAsync(factory, lessonId, cancellationToken).ConfigureAwait(false);
        return lessonId;
    }
}
