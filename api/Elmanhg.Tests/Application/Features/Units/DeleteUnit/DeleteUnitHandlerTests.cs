using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Units.DeleteUnit;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Units.DeleteUnit;

public sealed class DeleteUnitHandlerTests
{
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly CurriculumUnit _unit = CurriculumUnit.Create(Subject.Create("Physics", 1, Guid.NewGuid()), "Mechanics", 1, Guid.NewGuid());
    private readonly DeleteUnitHandler _handler;

    public DeleteUnitHandlerTests()
    {
        _currentUserService.UserId.Returns(_currentUserId);
        _unitRepository.GetByIdAsync(_unit.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<bool>()).Returns(_unit);
        _handler = new DeleteUnitHandler(_unitRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_ExistingUnit_SoftDeletesAndSaves()
    {
        await _handler.Handle(new DeleteUnitCommand(_unit.SubjectId, _unit.Id), TestContext.Current.CancellationToken);

        _unit.IsDeleted.Should().BeTrue();
        _unit.UpdatedBy.Should().Be(_currentUserId);
        await _unitRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnitNotFound_ThrowsUnitNotFound()
    {
        var act = () => _handler.Handle(new DeleteUnitCommand(_unit.SubjectId, Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UnitNotFound);
        await _unitRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnitOfOtherSubject_ThrowsUnitNotFound()
    {
        var act = () => _handler.Handle(new DeleteUnitCommand(Guid.NewGuid(), _unit.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UnitNotFound);
        _unit.IsDeleted.Should().BeFalse();
        await _unitRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new DeleteUnitCommand(_unit.SubjectId, _unit.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _unitRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
