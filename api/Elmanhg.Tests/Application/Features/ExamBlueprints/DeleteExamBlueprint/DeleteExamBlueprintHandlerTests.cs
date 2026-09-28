using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.ExamBlueprints.DeleteExamBlueprint;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.ExamBlueprints.DeleteExamBlueprint;

public sealed class DeleteExamBlueprintHandlerTests
{
    private readonly IExamBlueprintRepository _examBlueprintRepository = Substitute.For<IExamBlueprintRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly ExamBlueprintBuilder _builder = new();
    private readonly DeleteExamBlueprintHandler _handler;

    public DeleteExamBlueprintHandlerTests()
    {
        _currentUserService.UserId.Returns(_currentUserId);
        _handler = new DeleteExamBlueprintHandler(_examBlueprintRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_UnitBlueprint_SoftDeletesAndSaves()
    {
        var blueprint = Stored(_builder.BuildForUnit());

        await _handler.Handle(new DeleteExamBlueprintCommand(blueprint.Id), TestContext.Current.CancellationToken);

        blueprint.IsDeleted.Should().BeTrue();
        blueprint.UpdatedBy.Should().Be(_currentUserId);
        await _examBlueprintRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SubjectDefault_ThrowsDefaultNotDeletable()
    {
        var blueprint = Stored(_builder.BuildDefault());

        var act = () => _handler.Handle(new DeleteExamBlueprintCommand(blueprint.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BusinessRuleViolationCoreException>()).Which.ErrorCode.Should().Be(DomainErrorCodes.ExamBlueprintDefaultNotDeletable);
        blueprint.IsDeleted.Should().BeFalse();
        await _examBlueprintRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NotFound_ThrowsExamBlueprintNotFound()
    {
        var act = () => _handler.Handle(new DeleteExamBlueprintCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.ExamBlueprintNotFound);
        await _examBlueprintRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        var blueprint = Stored(_builder.BuildForUnit());
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new DeleteExamBlueprintCommand(blueprint.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        blueprint.IsDeleted.Should().BeFalse();
        await _examBlueprintRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private ExamBlueprint Stored(ExamBlueprint blueprint)
    {
        _examBlueprintRepository.GetByIdAsync(blueprint.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<ExamBlueprint>, IQueryable<ExamBlueprint>>?>(), Arg.Any<bool>()).Returns(blueprint);
        return blueprint;
    }
}
