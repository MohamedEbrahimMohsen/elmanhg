using Elmanhg.Domain.Avatar;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;

namespace Elmanhg.Tests.Integration.Avatar;

public static class AvatarTestData
{
    public const string Route = "/api/avatar";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public static Task<HttpResponseMessage> PostMessageAsync(HttpClient client, object body) => client.PostAsJsonAsync($"{Route}/messages", body, CancellationToken);

    public static async Task SeedUsageAsync(ApiFactory factory, Guid studentId, int count, DateTimeOffset at)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        for (var index = 0; index < count; index++)
        {
            context.AvatarMessageUsages.Add(AvatarMessageUsage.Record(studentId, AvatarEntryPoint.Lesson, at));
        }

        await context.SaveChangesAsync(CancellationToken).ConfigureAwait(false);
    }

    public static Task<HttpResponseMessage> GetConversationsAsync(HttpClient client, string query) => client.GetAsync($"{Route}/conversations{query}", CancellationToken);

    public static Task<HttpResponseMessage> GetConversationAsync(HttpClient client, Guid conversationId) => client.GetAsync($"{Route}/conversations/{conversationId}", CancellationToken);

    public static async Task SeedConversationAsync(ApiFactory factory, AvatarConversation conversation)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.AvatarConversations.Add(conversation);
        await context.SaveChangesAsync(CancellationToken).ConfigureAwait(false);
    }

    public static async Task<AvatarConversation?> ReadConversationAsync(ApiFactory factory, Guid conversationId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.AvatarConversations.AsNoTracking().Include(x => x.Messages).SingleOrDefaultAsync(x => x.Id == conversationId, CancellationToken).ConfigureAwait(false);
    }

    public static async Task<List<AvatarMessageUsage>> ReadUsageAsync(ApiFactory factory, Guid studentId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.AvatarMessageUsages.AsNoTracking().Where(x => x.StudentId == studentId).ToListAsync(CancellationToken).ConfigureAwait(false);
    }
}
