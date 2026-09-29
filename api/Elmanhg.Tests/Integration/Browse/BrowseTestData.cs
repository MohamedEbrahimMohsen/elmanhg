using Elmanhg.Domain.Lessons;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.Browse;

public static class BrowseTestData
{
    public const string Route = "/api/browse";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public static async Task<JsonElement> GetAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync($"{Route}/{path}", CancellationToken).ConfigureAwait(false);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken).ConfigureAwait(false);
    }

    public static Task<HttpResponseMessage> OpenAsync(HttpClient client, Guid lessonId) => client.PostAsync($"{Route}/lessons/{lessonId}/openings", null, CancellationToken);

    public static async Task<List<LessonOpening>> ReadOpeningsAsync(ApiFactory factory, Guid studentId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.LessonOpenings.AsNoTracking().Where(x => x.StudentId == studentId).ToListAsync(CancellationToken).ConfigureAwait(false);
    }
}
