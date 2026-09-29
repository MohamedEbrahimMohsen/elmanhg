using Elmanhg.Application.Subscriptions.GetLapsedSubscriptionIds;
using Elmanhg.Application.Subscriptions.LapseSubscription;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Integration.Subscriptions;

public sealed class SubscriptionLapseSweepTests(ApiFactory factory)
{
    private static readonly DateTimeOffset LongAgo = DateTimeOffset.UtcNow.AddYears(-20);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Send_GetLapsedIds_ReturnsDueSubscriptionsOnly()
    {
        var active = await SeedAsync(SubscriptionStatus.Active, LongAgo);
        var pastDue = await SeedAsync(SubscriptionStatus.PastDue, LongAgo.AddDays(1));
        var cancelled = await SeedAsync(SubscriptionStatus.Cancelled, LongAgo.AddDays(2));
        var current = await SeedAsync(SubscriptionStatus.Active, DateTimeOffset.UtcNow.AddDays(-5));

        var result = await SendAsync(new GetLapsedSubscriptionIdsQuery([]));

        result.Should().Contain([active.Id, pastDue.Id, cancelled.Id]).And.NotContain(current.Id);
    }

    [Fact]
    public async Task Send_GetLapsedIds_SkipsExcludedIds()
    {
        var excluded = await SeedAsync(SubscriptionStatus.Active, LongAgo.AddDays(-2));
        var due = await SeedAsync(SubscriptionStatus.Active, LongAgo.AddDays(-1));

        var result = await SendAsync(new GetLapsedSubscriptionIdsQuery([excluded.Id]));

        result.Should().Contain(due.Id).And.NotContain(excluded.Id);
    }

    [Fact]
    public async Task Send_LapseSubscription_PastGrace_PersistsExpired()
    {
        var subscription = await SeedAsync(SubscriptionStatus.PastDue, DateTimeOffset.UtcNow.AddMonths(-3));

        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new LapseSubscriptionCommand(subscription.Id), CancellationToken);
        }

        using var readScope = factory.Services.CreateScope();
        var stored = await readScope.ServiceProvider.GetRequiredService<AppDbContext>().Subscriptions.AsNoTracking().SingleAsync(x => x.Id == subscription.Id, CancellationToken);
        (stored.Status, stored.ExpiredAt).Should().Be((SubscriptionStatus.Expired, (DateTimeOffset?)(stored.CurrentPeriodEnd + TimeSpan.FromDays(3))));
    }

    private async Task<Subscription> SeedAsync(SubscriptionStatus status, DateTimeOffset start)
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken).ConfigureAwait(false);
        var subscription = new SubscriptionBuilder().ForStudent(student.Id).InStatus(status).StartingAt(start).Build();
        await SubscriptionTestData.SeedSubscriptionAsync(factory, subscription, CancellationToken).ConfigureAwait(false);
        return subscription;
    }

    private async Task<List<Guid>> SendAsync(GetLapsedSubscriptionIdsQuery query)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(query, CancellationToken).ConfigureAwait(false);
    }
}
