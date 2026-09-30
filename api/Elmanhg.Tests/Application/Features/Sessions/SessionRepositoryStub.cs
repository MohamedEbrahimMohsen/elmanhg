using Elmanhg.Domain.Sessions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Sessions;

public static class SessionRepositoryStub
{
    public static void StubFind(ISessionRepository repository, params Session[] sessions)
    {
        repository.FirstOrDefaultAsync(Arg.Any<Expression<Func<Session, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Session>, IQueryable<Session>>?>(), Arg.Any<Func<IQueryable<Session>, IOrderedQueryable<Session>>?>(), Arg.Any<bool>())
            .Returns(call => sessions.FirstOrDefault(call.Arg<Expression<Func<Session, bool>>>().Compile()));
    }

    public static void StubCount(ISessionRepository repository, params Session[] sessions)
    {
        repository.CountAsync(Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Session, bool>>?>())
            .Returns(call => sessions.Count(call.ArgAt<Expression<Func<Session, bool>>?>(1)!.Compile()));
    }
}
