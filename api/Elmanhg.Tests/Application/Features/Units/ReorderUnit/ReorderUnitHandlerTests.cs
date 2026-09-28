using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Units.ReorderUnit;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Units.ReorderUnit;

public sealed class ReorderUnitHandlerTests
{
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Subject _subject = Subject.Create("Physics", 1, Guid.NewGuid());
    private readonly CurriculumUnit _first;
    private readonly CurriculumUnit _second;
    private readonly CurriculumUnit _third;
    private readonly ReorderUnitHandler _handler;

    public ReorderUnitHandlerTests()
    {
        _first = CurriculumUnit.Create(_subject, "A", 1, Guid.NewGuid());
        _second = CurriculumUnit.Create(_subject, "B", 2, Guid.NewGuid());
        _third = CurriculumUnit.Create(_subject, "C", 3, Guid.NewGuid());
        _currentUserService.UserId.Returns(Guid.NewGuid());
        _unitRepository.FindAsync(Arg.Any<Expression<Func<CurriculumUnit, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IOrderedQueryable<CurriculumUnit>>?>(), Arg.Any<bool>()).Returns(_ => [_first, _second, _third]);
        _handler = new ReorderUnitHandler(_unitRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_MoveFirstToLast_RenumbersSiblingsAndSaves()
    {
        await _handler.Handle(new ReorderUnitCommand(_subject.Id, _first.Id, 3), TestContext.Current.CancellationToken);

        _second.Order.Should().Be(1);
        _third.Order.Should().Be(2);
        _first.Order.Should().Be(3);
        await _unitRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnitNotInSubject_ThrowsUnitNotFound()
    {
        var act = () => _handler.Handle(new ReorderUnitCommand(_subject.Id, Guid.NewGuid(), 1), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UnitNotFound);
        await _unitRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new ReorderUnitCommand(_subject.Id, _first.Id, 3), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _unitRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
