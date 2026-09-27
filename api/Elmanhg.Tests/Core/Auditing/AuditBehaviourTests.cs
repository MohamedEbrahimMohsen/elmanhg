using Core.Auditing;
using Core.Auditing.Entities;
using Core.Auditing.Repositories;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Security.Claims;
using System.Text.Json.Nodes;

namespace Elmanhg.Tests.Core.Auditing;

public sealed record AuditProbeCommand(Guid? Id) : IRequest<AuditProbeResult>, IAuditableCommand
{
    public string AuditAction => "Probe.Act";
    public string AuditResourceType => "Probe";
    public Guid? AuditResourceId => Id;
}

public sealed record AuditProbeResult(Guid Id) : IAuditableResult
{
    Guid? IAuditableResult.AuditResourceId => Id;
}

public sealed record NonAuditedProbeRequest : IRequest<AuditProbeResult>;

public sealed class AuditBehaviourTests
{
    private readonly IAuditLogRepository _auditLogRepository = Substitute.For<IAuditLogRepository>();
    private readonly AuditChangeCollector _auditChangeCollector = new();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly AuditProbeResult _result = new(Guid.NewGuid());
    private readonly AuditEntityChange _change = new("TeacherSubject", Guid.NewGuid(), AuditChangeKind.Created, new Dictionary<string, AuditValueChange> { ["TeacherId"] = new(null, JsonValue.Create("t-1")) });
    private AuditLog? _entry;

    public AuditBehaviourTests()
    {
        _currentUserService.UserId.Returns(_actorId);
        _currentUserService.UserName.Returns("admin@elmanhg.test");
        _currentUserService.GetClaim(ClaimTypes.Role).Returns("Admin");
        _auditLogRepository.AppendAsync(Arg.Do<AuditLog>(entry => _entry = entry), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Handle_NonAuditableRequest_DoesNotAppend()
    {
        var behaviour = new AuditBehaviour<NonAuditedProbeRequest, AuditProbeResult>(_auditLogRepository, _auditChangeCollector, _currentUserService, NullLogger<AuditBehaviour<NonAuditedProbeRequest, AuditProbeResult>>.Instance);

        var response = await behaviour.Handle(new NonAuditedProbeRequest(), _ => Task.FromResult(_result), TestContext.Current.CancellationToken);

        response.Should().BeSameAs(_result);
        await _auditLogRepository.DidNotReceive().AppendAsync(Arg.Any<AuditLog>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AuditableSuccess_AppendsSuccessRowWithActorActionAndResource()
    {
        var command = new AuditProbeCommand(Guid.NewGuid());

        await Behaviour().Handle(command, _ => Task.FromResult(_result), TestContext.Current.CancellationToken);

        _entry.Should().NotBeNull();
        _entry!.ActorUserId.Should().Be(_actorId);
        _entry.ActorUserName.Should().Be("admin@elmanhg.test");
        _entry.ActorRole.Should().Be("Admin");
        _entry.Action.Should().Be("Probe.Act");
        _entry.ResourceType.Should().Be("Probe");
        _entry.ResourceId.Should().Be(command.Id);
        _entry.Outcome.Should().Be("Success");
        _entry.ErrorCode.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ChangesRecordedByHandler_AppendsThemAsDiff()
    {
        await Behaviour().Handle(new AuditProbeCommand(Guid.NewGuid()), _ => RecordAndReturn([_change]), TestContext.Current.CancellationToken);

        _entry!.Diff.Should().Be(AuditDiff.Serialize([_change]));
    }

    [Fact]
    public async Task Handle_ChangesRecordedBeforeHandler_ExcludedFromDiff()
    {
        _auditChangeCollector.Record([_change]);

        await Behaviour().Handle(new AuditProbeCommand(Guid.NewGuid()), _ => RecordAndReturn([]), TestContext.Current.CancellationToken);

        _entry!.Diff.Should().BeNull();
    }

    [Fact]
    public async Task Handle_HandlerThrowsCoreException_AppendsFailureRowAndRethrows()
    {
        var exception = new NotFoundCoreException(ErrorCodes.SubjectNotFound);

        var act = () => Behaviour().Handle(new AuditProbeCommand(Guid.NewGuid()), _ => throw exception, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.Should().BeSameAs(exception);
        _entry!.Outcome.Should().Be("Failure");
        _entry.ErrorCode.Should().Be(ErrorCodes.SubjectNotFound);
    }

    [Fact]
    public async Task Handle_CommandWithoutResourceId_TakesIdFromAuditableResult()
    {
        await Behaviour().Handle(new AuditProbeCommand(null), _ => Task.FromResult(_result), TestContext.Current.CancellationToken);

        _entry!.ResourceId.Should().Be(_result.Id);
    }

    [Fact]
    public async Task Handle_AppendFails_StillReturnsResponse()
    {
        _auditLogRepository.AppendAsync(Arg.Any<AuditLog>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("database down"));

        var response = await Behaviour().Handle(new AuditProbeCommand(Guid.NewGuid()), _ => Task.FromResult(_result), TestContext.Current.CancellationToken);

        response.Should().BeSameAs(_result);
    }

    [Fact]
    public async Task Handle_AuditableRequest_AppendsWithUncancelledToken()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Behaviour().Handle(new AuditProbeCommand(Guid.NewGuid()), _ => Task.FromResult(_result), cancellation.Token);

        await _auditLogRepository.Received(1).AppendAsync(Arg.Any<AuditLog>(), CancellationToken.None);
    }

    private AuditBehaviour<AuditProbeCommand, AuditProbeResult> Behaviour() => new(_auditLogRepository, _auditChangeCollector, _currentUserService, NullLogger<AuditBehaviour<AuditProbeCommand, AuditProbeResult>>.Instance);

    private Task<AuditProbeResult> RecordAndReturn(IReadOnlyList<AuditEntityChange> changes)
    {
        _auditChangeCollector.Record(changes);
        return Task.FromResult(_result);
    }
}
