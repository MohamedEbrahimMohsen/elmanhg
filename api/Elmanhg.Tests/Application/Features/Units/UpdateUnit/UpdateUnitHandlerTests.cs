using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Units.UpdateUnit;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Units.UpdateUnit;

public sealed class UpdateUnitHandlerTests
{
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly CurriculumUnit _unit = CurriculumUnit.Create(Subject.Create("Physics", 1, Guid.NewGuid()), "Mechanics", 1, Guid.NewGuid());
    private readonly UpdateUnitHandler _handler;

    public UpdateUnitHandlerTests()
    {
        _currentUserService.UserId.Returns(_currentUserId);
        _unitRepository.GetByIdAsync(_unit.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<bool>()).Returns(_unit);
        _handler = new UpdateUnitHandler(_unitRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_ExistingUnit_RenamesAndSaves()
    {
        await _handler.Handle(new UpdateUnitCommand(_unit.SubjectId, _unit.Id, "Waves"), TestContext.Current.CancellationToken);

        _unit.Name.Should().Be("Waves");
        _unit.UpdatedBy.Should().Be(_currentUserId);
        await _unitRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnitNotFound_ThrowsUnitNotFound()
    {
        var act = () => _handler.Handle(new UpdateUnitCommand(_unit.SubjectId, Guid.NewGuid(), "Waves"), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UnitNotFound);
        await _unitRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnitOfOtherSubject_ThrowsUnitNotFound()
    {
        var act = () => _handler.Handle(new UpdateUnitCommand(Guid.NewGuid(), _unit.Id, "Waves"), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UnitNotFound);
        _unit.Name.Should().Be("Mechanics");
        await _unitRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new UpdateUnitCommand(_unit.SubjectId, _unit.Id, "Waves"), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _unitRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
