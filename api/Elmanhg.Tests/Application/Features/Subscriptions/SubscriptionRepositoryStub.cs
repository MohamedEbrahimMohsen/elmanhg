using Elmanhg.Domain.Subscriptions;
using Elmanhg.Tests.Builders;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Subscriptions;

public static class SubscriptionRepositoryStub
{
    public static void Stub(ISubscriptionRepository repository, params Subscription[] subscriptions)
    {
        repository.FindAsync(Arg.Any<Expression<Func<Subscription, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subscription>, IQueryable<Subscription>>?>(), Arg.Any<Func<IQueryable<Subscription>, IOrderedQueryable<Subscription>>?>(), Arg.Any<bool>())
            .Returns(call => subscriptions.Where(call.Arg<Expression<Func<Subscription, bool>>>().Compile()).ToList());
    }

    public static Subscription EntitledBase(Guid studentId, DateTimeOffset now) => new SubscriptionBuilder().ForStudent(studentId).StartingAt(now.AddDays(-1)).Build();
}
