using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Infrastructure.Data.Context;

namespace Elmanhg.Infrastructure.Subscriptions;

public class SubscriptionRepository(AppDbContext context) : Repository<Subscription>(context), ISubscriptionRepository { }
