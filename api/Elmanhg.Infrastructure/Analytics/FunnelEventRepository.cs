using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.Analytics;
using Elmanhg.Infrastructure.Data.Context;

namespace Elmanhg.Infrastructure.Analytics;

public class FunnelEventRepository(AppDbContext context) : Repository<FunnelEvent>(context), IFunnelEventRepository { }
