using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.Identity;
using Elmanhg.Infrastructure.Data.Context;

namespace Elmanhg.Infrastructure.Identity;

public class IssuedRefreshTokenRepository(AppDbContext context) : Repository<IssuedRefreshToken>(context), IIssuedRefreshTokenRepository { }
