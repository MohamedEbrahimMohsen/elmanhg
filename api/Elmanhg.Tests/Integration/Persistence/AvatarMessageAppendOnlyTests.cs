using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Avatar;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Elmanhg.Tests.Integration.Persistence;

public sealed class AvatarMessageAppendOnlyTests(ApiFactory factory)
{
    [Fact]
    public async Task Update_AvatarMessageRow_RejectedByDatabase()
    {
        var (conversationId, messageId) = await SeedMessageAsync();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var act = () => context.Database.ExecuteSqlAsync($"UPDATE \"AvatarMessages\" SET \"Text\" = {"changed"} WHERE \"Id\" = {messageId}", TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("P0001");
        (await AvatarTestData.ReadConversationAsync(factory, conversationId))!.Messages.Single(x => x.Id == messageId).Text.Should().Be("question");
    }

    [Fact]
    public async Task Delete_AvatarMessageRow_RejectedByDatabase()
    {
        var (conversationId, messageId) = await SeedMessageAsync();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var act = () => context.Database.ExecuteSqlAsync($"DELETE FROM \"AvatarMessages\" WHERE \"Id\" = {messageId}", TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("P0001");
        (await AvatarTestData.ReadConversationAsync(factory, conversationId))!.Messages.Should().Contain(x => x.Id == messageId);
    }

    private async Task<(Guid ConversationId, Guid MessageId)> SeedMessageAsync()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var conversation = new AvatarConversationBuilder().ForStudent(student.Id).WithExchange("question", "reply").Build();
        await AvatarTestData.SeedConversationAsync(factory, conversation).ConfigureAwait(false);
        return (conversation.Id, conversation.Messages[0].Id);
    }
}
