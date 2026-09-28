using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Subjects.CreateSubject;
using Elmanhg.Domain.Subjects;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Subjects.CreateSubject;

public sealed class CreateSubjectHandlerTests
{
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly CreateSubjectHandler _handler;
    private Subject? _added;

    public CreateSubjectHandlerTests()
    {
        _currentUserService.UserId.Returns(_currentUserId);
        _subjectRepository.AddAsync(Arg.Do<Subject>(x => _added = x), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _handler = new CreateSubjectHandler(_subjectRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_ExistingSubjects_AddsSubjectAfterLastAndSaves()
    {
        ArrangeLast(Subject.Create("Biology", 3, Guid.NewGuid()));

        var result = await _handler.Handle(new CreateSubjectCommand("Physics"), TestContext.Current.CancellationToken);

        _added.Should().NotBeNull();
        _added!.Order.Should().Be(4);
        _added.Name.Should().Be("Physics");
        _added.CreatedBy.Should().Be(_currentUserId);
        result.Id.Should().Be(_added.Id);
        await _subjectRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoSubjects_AddsSubjectAtOrderOne()
    {
        ArrangeLast(null);

        await _handler.Handle(new CreateSubjectCommand("Physics"), TestContext.Current.CancellationToken);

        _added!.Order.Should().Be(1);
        await _subjectRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new CreateSubjectCommand("Physics"), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _subjectRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private void ArrangeLast(Subject? last)
    {
        _subjectRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<Subject, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<Func<IQueryable<Subject>, IOrderedQueryable<Subject>>?>(), Arg.Any<bool>()).Returns(last);
    }
}
