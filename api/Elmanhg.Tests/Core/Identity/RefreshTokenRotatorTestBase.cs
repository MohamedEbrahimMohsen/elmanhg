using Core.Identity.Tokens;
using Core.Identity.Tokens.RefreshToken;
using Elmanhg.Domain.Identity;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Core.Identity;

public abstract class RefreshTokenRotatorTestBase
{
    protected const string OldRefreshToken = "old-refresh-token";
    protected const string NewRefreshToken = "new-refresh-token";
    protected const int GraceSeconds = 10;

    protected static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    protected readonly IRefreshTokenService<User, Guid> _refreshTokenService = Substitute.For<IRefreshTokenService<User, Guid>>();
    protected readonly IIssuedRefreshTokenRepository _issuedRefreshTokenRepository = Substitute.For<IIssuedRefreshTokenRepository>();
    protected readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    protected readonly List<IssuedRefreshToken> _records = [];
    protected readonly User _user = User.CreateStudentWithPhone("Ahmed", "01012345678");
    protected readonly RefreshTokenRotator<User> _rotator;
    protected Action<IssuedRefreshToken> _addIfAbsent;

    protected RefreshTokenRotatorTestBase()
    {
        _refreshTokenService.GenerateTokenAsync(Arg.Any<User>(), Arg.Any<CancellationToken>()).Returns(NewRefreshToken);
        _timeProvider.GetUtcNow().Returns(Now);
        _issuedRefreshTokenRepository.FindAsync(Arg.Any<Expression<Func<IssuedRefreshToken, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<IssuedRefreshToken>, IQueryable<IssuedRefreshToken>>?>(), Arg.Any<Func<IQueryable<IssuedRefreshToken>, IOrderedQueryable<IssuedRefreshToken>>?>(), Arg.Any<bool>())
            .Returns(call => _records.Where(call.Arg<Expression<Func<IssuedRefreshToken, bool>>>().Compile()).ToList());
        _issuedRefreshTokenRepository.When(x => x.AddAsync(Arg.Any<IssuedRefreshToken>(), Arg.Any<CancellationToken>())).Do(call => _records.Add(call.Arg<IssuedRefreshToken>()));
        _addIfAbsent = _records.Add;
        _issuedRefreshTokenRepository.When(x => x.AddIfAbsentAsync(Arg.Any<IssuedRefreshToken>(), Arg.Any<CancellationToken>())).Do(call => _addIfAbsent(call.Arg<IssuedRefreshToken>()));
        _rotator = Rotator(TimeSpan.FromSeconds(GraceSeconds));
    }

    protected RefreshTokenRotator<User> Rotator(TimeSpan reuseGrace) => new(_refreshTokenService, _issuedRefreshTokenRepository, Options.Create(new RefreshTokenRotationOptions { ReuseGrace = reuseGrace }), Options.Create(new JwtOptions { RefreshTokenExpirationDays = 7 }), _timeProvider);

    protected IssuedRefreshToken Single(string token) => _records.Single(x => x.TokenHash == RefreshTokenHash.Compute(token));

    protected IssuedRefreshToken Record(string token, Guid familyId, DateTimeOffset? rotatedAt)
    {
        var record = IssuedRefreshToken.Issue(_user.Id, familyId, RefreshTokenHash.Compute(token), Now.AddHours(-1), Now.AddDays(6));
        if (rotatedAt is { } at)
        {
            record.Rotate(at);
        }

        _records.Add(record);
        return record;
    }
}
