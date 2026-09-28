using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.Sessions;
using Elmanhg.Infrastructure.Data.Context;

namespace Elmanhg.Infrastructure.Sessions;

public class SessionRepository(AppDbContext context) : Repository<Session>(context), ISessionRepository { }
