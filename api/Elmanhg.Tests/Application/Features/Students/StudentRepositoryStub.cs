using Elmanhg.Domain.Identity;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Students;

public static class StudentRepositoryStub
{
    public static void Stub(IUserRepository repository, params User[] users)
    {
        repository.FirstOrDefaultAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<User>, IQueryable<User>>?>(), Arg.Any<Func<IQueryable<User>, IOrderedQueryable<User>>?>(), Arg.Any<bool>())
            .Returns(call => users.FirstOrDefault(call.Arg<Expression<Func<User, bool>>>().Compile()));
    }
}
