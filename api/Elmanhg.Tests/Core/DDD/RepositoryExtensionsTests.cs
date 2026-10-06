using Core.DDD.Entities;
using Core.DDD.Repositories;
using Core.Errors;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Core.DDD;

public sealed class RepositoryExtensionsTests
{
    private const string ProbeCode = "PROBE_NOT_FOUND";

    private readonly IRepository<ProbeEntity> _repository = Substitute.For<IRepository<ProbeEntity>>();

    [Fact]
    public async Task GetRequiredAsync_IdFound_ReturnsEntity()
    {
        var entity = new ProbeEntity(Guid.NewGuid());
        _repository.GetByIdAsync(entity.Id, Arg.Any<CancellationToken>()).Returns(entity);

        var result = await _repository.GetRequiredAsync(entity.Id, ProbeCode, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(entity);
    }

    [Fact]
    public async Task GetRequiredAsync_IdMissing_ThrowsNotFoundWithGivenCode()
    {
        var act = () => _repository.GetRequiredAsync(Guid.NewGuid(), ProbeCode, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ProbeCode);
    }

    [Fact]
    public async Task GetRequiredAsync_IdWithIncludeAndNoTracking_ForwardsBoth()
    {
        var entity = new ProbeEntity(Guid.NewGuid());
        Func<IQueryable<ProbeEntity>, IQueryable<ProbeEntity>> include = query => query.Where(x => !x.IsDeleted);
        _repository.GetByIdAsync(entity.Id, Arg.Any<CancellationToken>(), include, true).Returns(entity);

        var result = await _repository.GetRequiredAsync(entity.Id, ProbeCode, TestContext.Current.CancellationToken, include, asNoTracking: true);

        result.Should().BeSameAs(entity);
        await _repository.Received(1).GetByIdAsync(entity.Id, Arg.Any<CancellationToken>(), include, true);
    }

    [Fact]
    public async Task GetRequiredAsync_PredicateFound_ReturnsEntity()
    {
        var entity = new ProbeEntity(Guid.NewGuid());
        Expression<Func<ProbeEntity, bool>> predicate = x => x.Id == entity.Id;
        _repository.FirstOrDefaultAsync(predicate, Arg.Any<CancellationToken>()).Returns(entity);

        var result = await _repository.GetRequiredAsync(predicate, ProbeCode, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(entity);
    }

    [Fact]
    public async Task GetRequiredAsync_PredicateMissing_ThrowsNotFoundWithGivenCode()
    {
        var act = () => _repository.GetRequiredAsync(x => x.IsDeleted, ProbeCode, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ProbeCode);
    }

    [Fact]
    public async Task GetRequiredAsync_PredicateWithOrderByAndNoTracking_ForwardsAll()
    {
        var entity = new ProbeEntity(Guid.NewGuid());
        Expression<Func<ProbeEntity, bool>> predicate = x => !x.IsDeleted;
        Func<IQueryable<ProbeEntity>, IOrderedQueryable<ProbeEntity>> orderBy = query => query.OrderBy(x => x.Id);
        _repository.FirstOrDefaultAsync(predicate, Arg.Any<CancellationToken>(), null, orderBy, true).Returns(entity);

        var result = await _repository.GetRequiredAsync(predicate, ProbeCode, TestContext.Current.CancellationToken, orderBy: orderBy, asNoTracking: true);

        result.Should().BeSameAs(entity);
        await _repository.Received(1).FirstOrDefaultAsync(predicate, Arg.Any<CancellationToken>(), null, orderBy, true);
    }

    public sealed class ProbeEntity(Guid id) : Entity(id);
}
