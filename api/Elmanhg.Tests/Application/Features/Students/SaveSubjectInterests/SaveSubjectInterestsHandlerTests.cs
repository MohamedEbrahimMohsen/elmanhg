using System.Linq.Expressions;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Students.SaveSubjectInterests;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Subjects;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Students.SaveSubjectInterests;

public sealed class SaveSubjectInterestsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly User _student = User.CreateStudentWithEmail("Sara", "sara@elmanhg.test");
    private readonly SaveSubjectInterestsHandler _handler;

    public SaveSubjectInterestsHandlerTests()
    {
        _currentUserService.UserId.Returns(_student.Id);
        _timeProvider.GetUtcNow().Returns(Now);
        _userRepository.GetByIdAsync(_student.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<User>, IQueryable<User>>?>(), Arg.Any<bool>()).Returns(_student);
        _handler = new SaveSubjectInterestsHandler(_userRepository, _subjectRepository, _timeProvider, _currentUserService);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new SaveSubjectInterestsCommand([]), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _userRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownSubject_ThrowsSubjectNotFound()
    {
        StubSubjectCount(1);

        var act = () => _handler.Handle(new SaveSubjectInterestsCommand([Guid.NewGuid(), Guid.NewGuid()]), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.SubjectNotFound);
        await _userRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        _student.SubjectInterestIds.Should().BeEmpty();
        _student.OnboardedAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_UserMissing_ThrowsUserNotFound()
    {
        _currentUserService.UserId.Returns(Guid.NewGuid());

        var act = () => _handler.Handle(new SaveSubjectInterestsCommand([]), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotFound);
        await _userRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_KnownSubjects_SavesInterestsAndOnboards()
    {
        var physics = Guid.NewGuid();
        var chemistry = Guid.NewGuid();
        StubSubjectCount(2);

        await _handler.Handle(new SaveSubjectInterestsCommand([physics, chemistry]), TestContext.Current.CancellationToken);

        _student.SubjectInterestIds.Should().Equal(physics, chemistry);
        _student.OnboardedAt.Should().Be(Now);
        await _userRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EmptyList_OnboardsWithoutCountingSubjects()
    {
        await _handler.Handle(new SaveSubjectInterestsCommand([]), TestContext.Current.CancellationToken);

        _student.OnboardedAt.Should().Be(Now);
        _student.SubjectInterestIds.Should().BeEmpty();
        await _subjectRepository.DidNotReceive().CountAsync(Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Subject, bool>>?>());
        await _userRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private void StubSubjectCount(int count) => _subjectRepository.CountAsync(Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Subject, bool>>?>()).Returns(count);
}
