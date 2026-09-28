using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Subjects.ReorderSubject;
using Elmanhg.Domain.Subjects;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Subjects.ReorderSubject;

public sealed class ReorderSubjectHandlerTests
{
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Subject _first = Subject.Create("A", 1, Guid.NewGuid());
    private readonly Subject _second = Subject.Create("B", 2, Guid.NewGuid());
    private readonly Subject _third = Subject.Create("C", 3, Guid.NewGuid());
    private readonly ReorderSubjectHandler _handler;

    public ReorderSubjectHandlerTests()
    {
        _currentUserService.UserId.Returns(Guid.NewGuid());
        _subjectRepository.FindAsync(Arg.Any<Expression<Func<Subject, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<Func<IQueryable<Subject>, IOrderedQueryable<Subject>>?>(), Arg.Any<bool>()).Returns(_ => [_first, _second, _third]);
        _handler = new ReorderSubjectHandler(_subjectRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_MoveLastToFirst_RenumbersSiblingsAndSaves()
    {
        await _handler.Handle(new ReorderSubjectCommand(_third.Id, 1), TestContext.Current.CancellationToken);

        _third.Order.Should().Be(1);
        _first.Order.Should().Be(2);
        _second.Order.Should().Be(3);
        await _subjectRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PositionBeyondCount_MovesSubjectToEnd()
    {
        await _handler.Handle(new ReorderSubjectCommand(_first.Id, 99), TestContext.Current.CancellationToken);

        _second.Order.Should().Be(1);
        _third.Order.Should().Be(2);
        _first.Order.Should().Be(3);
        await _subjectRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SubjectNotFound_ThrowsSubjectNotFound()
    {
        var act = () => _handler.Handle(new ReorderSubjectCommand(Guid.NewGuid(), 1), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.SubjectNotFound);
        await _subjectRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new ReorderSubjectCommand(_third.Id, 1), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _subjectRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
