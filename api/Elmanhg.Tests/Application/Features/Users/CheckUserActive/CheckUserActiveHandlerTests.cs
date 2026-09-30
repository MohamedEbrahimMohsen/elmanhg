using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Users.CheckUserActive;
using Elmanhg.Domain.Identity;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Users.CheckUserActive;

public sealed class CheckUserActiveHandlerTests : IDisposable
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());
    private readonly List<User> _users = [];

    public CheckUserActiveHandlerTests()
    {
        _userRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<User>, IQueryable<User>>?>(), Arg.Any<Func<IQueryable<User>, IOrderedQueryable<User>>?>(), Arg.Any<bool>())
            .Returns(call => _users.FirstOrDefault(call.Arg<Expression<Func<User, bool>>>().Compile()));
    }

    [Fact]
    public async Task Handle_ActiveUser_ReturnsTrueAndServesRepeatFromCache()
    {
        var user = Seed(suspended: false);
        var handler = Handler(cacheSeconds: 30);

        var first = await handler.Handle(new CheckUserActiveQuery(user.Id), TestContext.Current.CancellationToken);
        var second = await handler.Handle(new CheckUserActiveQuery(user.Id), TestContext.Current.CancellationToken);

        (first, second).Should().Be((true, true));
        await _userRepository.Received(1).FirstOrDefaultAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<User>, IQueryable<User>>?>(), Arg.Any<Func<IQueryable<User>, IOrderedQueryable<User>>?>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task Handle_SuspendedUser_ReturnsFalse()
    {
        var user = Seed(suspended: true);

        var active = await Handler(cacheSeconds: 30).Handle(new CheckUserActiveQuery(user.Id), TestContext.Current.CancellationToken);

        active.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_UnknownUser_ReturnsFalse()
    {
        Seed(suspended: false);

        var active = await Handler(cacheSeconds: 30).Handle(new CheckUserActiveQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        active.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_CacheDisabled_QueriesEveryTime()
    {
        var user = Seed(suspended: false);
        var handler = Handler(cacheSeconds: 0);

        await handler.Handle(new CheckUserActiveQuery(user.Id), TestContext.Current.CancellationToken);
        await handler.Handle(new CheckUserActiveQuery(user.Id), TestContext.Current.CancellationToken);

        await _userRepository.Received(2).FirstOrDefaultAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<User>, IQueryable<User>>?>(), Arg.Any<Func<IQueryable<User>, IOrderedQueryable<User>>?>(), Arg.Any<bool>());
    }

    public void Dispose() => _memoryCache.Dispose();

    private CheckUserActiveHandler Handler(int cacheSeconds) => new(_userRepository, _memoryCache, Options.Create(new UsersOptions { ActiveStatusCacheSeconds = cacheSeconds }));

    private User Seed(bool suspended)
    {
        var user = User.CreateTeacher("Teacher", "teacher@elmanhg.test");
        if (suspended)
        {
            user.Suspend();
        }

        _users.Add(user);
        return user;
    }
}
