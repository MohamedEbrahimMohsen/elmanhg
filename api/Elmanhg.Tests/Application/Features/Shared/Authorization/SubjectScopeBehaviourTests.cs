using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Authorization;
using Elmanhg.Domain.Teachers;
using FluentAssertions;
using MediatR;
using NSubstitute;
using System.Security.Claims;

namespace Elmanhg.Tests.Application.Features.Shared.Authorization;

public sealed record ScopedProbeRequest(Guid SubjectId) : IRequest<Unit>, ISubjectScopedRequest;

public sealed record UnscopedProbeRequest : IRequest<Unit>;

public sealed class SubjectScopeBehaviourTests
{
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly ITeacherSubjectRepository _teacherSubjectRepository = Substitute.For<ITeacherSubjectRepository>();
    private readonly Guid _teacherId = Guid.NewGuid();
    private readonly Guid _subjectId = Guid.NewGuid();
    private bool _nextCalled;

    public SubjectScopeBehaviourTests()
    {
        _currentUserService.UserId.Returns(_teacherId);
        _currentUserService.GetClaim(ClaimTypes.Role).Returns("Teacher");
    }

    [Fact]
    public async Task Handle_UnscopedRequest_CallsNextWithoutLookup()
    {
        var behaviour = new SubjectScopeBehaviour<UnscopedProbeRequest, Unit>(_currentUserService, _teacherSubjectRepository);

        await behaviour.Handle(new UnscopedProbeRequest(), Next, TestContext.Current.CancellationToken);

        _nextCalled.Should().BeTrue();
        await _teacherSubjectRepository.DidNotReceive().IsAssignedAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AdminOnScopedRequest_CallsNextWithoutLookup()
    {
        _currentUserService.GetClaim(ClaimTypes.Role).Returns("Admin");

        await ScopedBehaviour().Handle(new ScopedProbeRequest(_subjectId), Next, TestContext.Current.CancellationToken);

        _nextCalled.Should().BeTrue();
        await _teacherSubjectRepository.DidNotReceive().IsAssignedAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_StudentOnScopedRequest_CallsNextWithoutLookup()
    {
        _currentUserService.GetClaim(ClaimTypes.Role).Returns("Student");

        await ScopedBehaviour().Handle(new ScopedProbeRequest(_subjectId), Next, TestContext.Current.CancellationToken);

        _nextCalled.Should().BeTrue();
        await _teacherSubjectRepository.DidNotReceive().IsAssignedAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TeacherAssignedToSubject_CallsNext()
    {
        _teacherSubjectRepository.IsAssignedAsync(_teacherId, _subjectId, Arg.Any<CancellationToken>()).Returns(true);

        await ScopedBehaviour().Handle(new ScopedProbeRequest(_subjectId), Next, TestContext.Current.CancellationToken);

        _nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_TeacherNotAssigned_ThrowsSubjectOutOfScope()
    {
        var act = () => ScopedBehaviour().Handle(new ScopedProbeRequest(_subjectId), Next, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ForbiddenCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.SubjectOutOfScope);
        _nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_MissingRoleClaimNotAssigned_ThrowsSubjectOutOfScope()
    {
        _currentUserService.GetClaim(ClaimTypes.Role).Returns((string?)null);

        var act = () => ScopedBehaviour().Handle(new ScopedProbeRequest(_subjectId), Next, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ForbiddenCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.SubjectOutOfScope);
        _nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_TeacherWithoutUserId_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => ScopedBehaviour().Handle(new ScopedProbeRequest(_subjectId), Next, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        _nextCalled.Should().BeFalse();
    }

    private SubjectScopeBehaviour<ScopedProbeRequest, Unit> ScopedBehaviour() => new(_currentUserService, _teacherSubjectRepository);

    private Task<Unit> Next(CancellationToken cancellationToken)
    {
        _nextCalled = true;
        return Unit.Task;
    }
}
