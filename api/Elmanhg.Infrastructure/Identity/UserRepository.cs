using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.Identity;
using Elmanhg.Infrastructure.Data.Context;

namespace Elmanhg.Infrastructure.Identity;

public class UserRepository(AppDbContext context) : Repository<User>(context), IUserRepository
{
}
