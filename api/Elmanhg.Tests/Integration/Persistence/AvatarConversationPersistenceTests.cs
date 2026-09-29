using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Avatar;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Integration.Persistence;

public sealed class AvatarConversationPersistenceTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SaveChanges_ConcurrentExchanges_ThrowsAvatarConversationModifiedConcurrently()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var conversation = new AvatarConversationBuilder().ForStudent(student.Id).WithExchange("q1", "r1").Build();
        await AvatarTestData.SeedConversationAsync(factory, conversation);
        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();
        var firstContext = firstScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var firstCopy = await firstContext.AvatarConversations.Include(x => x.Messages).SingleAsync(x => x.Id == conversation.Id, CancellationToken);
        var secondCopy = await secondContext.AvatarConversations.Include(x => x.Messages).SingleAsync(x => x.Id == conversation.Id, CancellationToken);
        var now = DateTimeOffset.UtcNow;
        firstCopy.RecordExchange("q2", AvatarConversationBuilder.Reply("r2"), now, now);
        secondCopy.RecordExchange("q2 again", AvatarConversationBuilder.Reply("r2 again"), now, now);
        await firstContext.SaveChangesAsync(CancellationToken);

        var act = () => secondContext.SaveChangesAsync(CancellationToken);

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.AvatarConversationModifiedConcurrently);
        (await AvatarTestData.ReadConversationAsync(factory, conversation.Id))!.Messages.Should().HaveCount(4);
    }
}
